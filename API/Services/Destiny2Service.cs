using API.Clients.Abstract;
using API.Models.Responses;
using API.Services.Abstract;
using Domain.Data;
using Domain.DB;
using Domain.DestinyApi;
using Domain.Manifest;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
using System.Collections.Concurrent;

namespace API.Services
{
    public class Destiny2Service : IDestiny2Service
    {
        private readonly IDestiny2ApiClient _client;
        private readonly IManifestClient _manifestClient;
        private readonly IDatabase _cache;
        private readonly AppDbContext _context;

        private static readonly DateTime ActivityCutoffUtc = new(2025, 7, 15, 0, 0, 0, DateTimeKind.Utc);

        public Destiny2Service(IDestiny2ApiClient client, IManifestClient manifestClient, IConnectionMultiplexer redis, AppDbContext context)
        {
            _client = client;
            _manifestClient = manifestClient;
            _cache = redis.GetDatabase();
            _context = context;
        }

        public async Task<List<PlayerSearchDto>> SearchForPlayer(string playerName)
        {
            var hasBungieId = playerName.Length > 5 && playerName[^5] == '#';

            var response = hasBungieId ?
                await SearchByBungieName(playerName) :
                await SearchByPrefix(playerName);

            var filteredMemberships = response
                    .Where(m => m.applicableMembershipTypes.Count > 0)
                    .Select(m => new PlayerSearchDto
                    {
                        Id = long.Parse(m.membershipId),
                        MembershipType = m.membershipType,
                        FullDisplayName = m.bungieGlobalDisplayName + "#" + m.bungieGlobalDisplayNameCode
                    })
                    .DistinctBy(r => r.Id)
                    .ToList(); //because apparently sometimes there are dupes

            await AddSearchResultsToDb(filteredMemberships);

            return filteredMemberships;
        }

        private async Task AddSearchResultsToDb(List<PlayerSearchDto> result)
        {
            if (result is null || result.Count == 0)
                return;

            var distinct = result
                .GroupBy(r => (r.Id, r.MembershipType))
                .Select(g => g.First())
                .ToList();

            var membershipIds = distinct.Select(r => r.Id).Distinct().ToList();
            var membershipTypes = distinct.Select(r => r.MembershipType).Distinct().ToList();

            var existingKeys = await _context.Players
                .AsNoTracking()
                .Where(p => membershipIds.Contains(p.Id) && membershipTypes.Contains(p.MembershipType))
                .Select(p => new ValueTuple<long, int>(p.Id, p.MembershipType))
                .ToListAsync();

            var existingSet = existingKeys.ToHashSet();

            var newPlayers = new List<Player>(distinct.Count);

            foreach (var membership in distinct)
            {
                var key = (membership.Id, membership.MembershipType);
                if (existingSet.Contains(key))
                    continue;

                var full = membership.FullDisplayName;
                int displayNameCode = 0;
                string displayName = full;

                var hashPos = full.LastIndexOf('#');
                if (hashPos > 0 && hashPos < full.Length - 1)
                {
                    displayName = full[..hashPos];
                    _ = int.TryParse(full[(hashPos + 1)..], out displayNameCode);
                }

                newPlayers.Add(new Player
                {
                    Id = membership.Id,
                    MembershipType = membership.MembershipType,
                    DisplayName = displayName,
                    DisplayNameCode = displayNameCode,
                    LastUpdateStatus = "N/A"
                });
            }

            if (newPlayers.Count > 0)
            {
                _context.Players.AddRange(newPlayers);
                _cache.KeyDelete("players:all");
                await _context.SaveChangesAsync();
            }
        }

        private async Task<IEnumerable<UserInfoCard>> SearchByBungieName(string playerName)
        {
            var bungieId = int.Parse(playerName[^4..]);
            playerName = playerName[..^5];

            var player = new ExactSearchRequest
            {
                displayName = playerName,
                displayNameCode = bungieId
            };

            var response = await _client.PerformSearchByBungieName(player, -1);

            var results = response.Response;

            return results;
        }

        private async Task<IEnumerable<UserInfoCard>> SearchByPrefix(string playerName)
        {
            var page = 0;
            var player = new UserSearchPrefixRequest
            {
                displayNamePrefix = playerName
            };
            var response = await _client.PerformSearchByPrefix(player, page);
            var hasMore = response.Response.hasMore;
            while (hasMore)
            {
                page++;
                var nextResponse = await _client.PerformSearchByPrefix(player, page);
                response.Response.searchResults.AddRange(nextResponse.Response.searchResults);
                hasMore = nextResponse.Response.hasMore;
            }

            return response.Response.searchResults.SelectMany(r => r.destinyMemberships);
        }

        public async Task<Dictionary<string, DestinyCharacterComponent>> GetCharactersForPlayer(long membershipId, int membershipType)
        {
            try
            {
                var characters = await _client.GetCharactersForPlayer(membershipId, membershipType);
                if (characters.ErrorCode == 1665)
                {
                    // user is private
                    return new Dictionary<string, DestinyCharacterComponent>();
                }
                await CheckPlayerName(characters.Response, membershipId);
                return characters.Response.characters.data;
            }
            catch (Exception ex)
            {
                throw;
            }
        }

        public async Task CheckPlayerName(DestinyProfileResponse profile, long id)
        {
            var player = await _context.Players.FirstOrDefaultAsync(p => p.Id == id);
            if (player == null)
                return;
            if (player.DisplayName != profile.profile.data.userInfo.bungieGlobalDisplayName ||
                player.DisplayNameCode != profile.profile.data.userInfo.bungieGlobalDisplayNameCode)
            {
                player.DisplayName = profile.profile.data.userInfo.bungieGlobalDisplayName;
                player.DisplayNameCode = profile.profile.data.userInfo.bungieGlobalDisplayNameCode;
                _context.Players.Update(player);
                await _context.SaveChangesAsync();
            }
        }

        public async Task LoadPlayerActivityReports(Player player, DateTime lastPlayedActivityDate, string characterId)
        {
            var entries = await _cache.HashGetAllAsync("activityHashMappings");
            var activityHashMap = entries.ToDictionary(
                x => long.TryParse(x.Name, out var nameHash) ? nameHash : 0,
                x => long.TryParse(x.Value, out var valueHash) ? valueHash : 0
            );

            player.LastUpdateStatus = "Updating";
            player.LastUpdateStarted = DateTime.UtcNow;
            _context.Players.Update(player);
            await _context.SaveChangesAsync();

            var page = 0;
            var reportsToAdd = new List<ActivityReport>();
            var hasReachedLastUpdate = false;
            var searchToDate = player.NeedsFullCheck ? ActivityCutoffUtc : lastPlayedActivityDate;

            var activityCount = 250;

            Task<DestinyApiResponse<DestinyActivityHistoryResults>> inFlight =
            _client.GetHistoricalStatsForCharacter(player.Id, player.MembershipType, characterId, page, activityCount);

            try
            {
                while (!hasReachedLastUpdate)
                {
                    var newReportIds = new List<long>();

                    var response = await inFlight;
                    if (response.ErrorCode != 1 || response.Response?.activities == null || !response.Response.activities.Any())
                        break;

                    page++;
                    var prefetchNext = _client.GetHistoricalStatsForCharacter(player.Id, player.MembershipType, characterId, page, activityCount);

                    foreach (var activity in response.Response.activities)
                    {
                        hasReachedLastUpdate = activity.period <= (searchToDate);
                        if (activity.period < ActivityCutoffUtc || hasReachedLastUpdate)
                            break;

                        var rawHash = activity.activityDetails.referenceId;
                        if (!activityHashMap.TryGetValue(rawHash, out var canonicalId))
                            continue;

                        if (!long.TryParse(activity.activityDetails.instanceId, out var instanceId))
                            continue;

                        var existing = await _context.ActivityReports
                            .Include(ar => ar.Players)
                            .FirstOrDefaultAsync(ar => ar.Id == instanceId);

                        if (existing is null)
                        {
                            newReportIds.Add(instanceId);

                            reportsToAdd.Add(new ActivityReport
                            {
                                Id = instanceId,
                                ActivityId = canonicalId,
                                Date = activity.period,
                                NeedsFullCheck = activity.values["playerCount"].basic.value != 1,
                                Players = new List<ActivityReportPlayer>()
                                {
                                    new ActivityReportPlayer
                                    {
                                        PlayerId = player.Id,
                                        Score = (int)activity.values["score"].basic.value,
                                        Completed = activity.values["completed"].basic.value == 1 && activity.values["completionReason"].basic.value != 2.0,
                                        Duration = TimeSpan.FromSeconds(activity.values["activityDurationSeconds"].basic.value)
                                    }
                                }
                            });
                        }
                        else if (existing.NeedsFullCheck && !existing.Players.Any(p => p.PlayerId == player.Id))
                        {
                            existing.Players.Add(new ActivityReportPlayer
                            {
                                PlayerId = player.Id,
                                Score = (int)activity.values["score"].basic.value,
                                Completed = activity.values["completed"].basic.value == 1 && activity.values["completionReason"].basic.value != 2.0,
                                Duration = TimeSpan.FromSeconds(activity.values["activityDurationSeconds"].basic.value)
                            });
                            existing.NeedsFullCheck = existing.Players.Count != activity.values["playerCount"].basic.value;
                        }
                    }

                    if (reportsToAdd.Any())
                    {
                        _context.ActivityReports.AddRange(reportsToAdd);
                        await _context.SaveChangesAsync();
                        reportsToAdd.Clear();
                    }

                    if (response.Response.activities.Last().period < ActivityCutoffUtc)
                        break;

                    inFlight = prefetchNext;
                }

                player.LastUpdateStatus = "Complete";
                player.LastUpdateCompleted = DateTime.UtcNow;
                player.NeedsFullCheck = false;
                _context.Players.Update(player);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                if (await _context.Players.AsNoTracking().AnyAsync(p => p.Id == player.Id))
                {
                    player.LastUpdateStatus = $"Error: {ex.Message}";
                    player.LastUpdateCompleted = DateTime.UtcNow;
                    _context.Players.Update(player);
                    try { await _context.SaveChangesAsync(); } catch { /* ignore */ }
                }
                throw;
            }
        }



        public async Task GroupActivityDuplicates()
        {
            var canonicalActivities = await _context.Activities.ToListAsync();
            var manifest = await _client.GetManifest();
            var allActivities = await _manifestClient.GetActivityDefinitions(manifest.Response.jsonWorldComponentContentPaths.en.DestinyActivityDefinition);

            var canonicalNames = canonicalActivities
                .Select(a => NormalizeActivityName(a.Name))
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var grouped = allActivities.Values
                .Where(d => canonicalNames.Contains(NormalizeActivityName(d.displayProperties.name)))
                .GroupBy(d => NormalizeActivityName(d.displayProperties.name))
                .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

            var mappings = BuildCanonicalModelsAndMappings(grouped, canonicalActivities);

            await _cache.HashSetAsync("activityHashMappings",
                mappings.Select(kv => new HashEntry(kv.Key, kv.Value)).ToArray());
        }

        private static string NormalizeActivityName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return name;
            var idx = name.IndexOf(": Customize", StringComparison.OrdinalIgnoreCase);
            if (idx == -1)
                idx = name.IndexOf(": Matchmade", StringComparison.OrdinalIgnoreCase);
            return (idx >= 0 ? name[..idx] : name).Trim();
        }

        private static Dictionary<long, long> BuildCanonicalModelsAndMappings(
            Dictionary<string, List<DestinyActivityDefinition>> grouped,
            IReadOnlyCollection<Activity> canonicalActivities)
        {
            var mappings = new Dictionary<long, long>();

            foreach (var canonical in canonicalActivities)
            {
                var normalizedName = NormalizeActivityName(canonical.Name);

                if (!string.IsNullOrWhiteSpace(normalizedName) && grouped.TryGetValue(normalizedName, out var variants) && variants.Any())
                {
                    var canonicalDef = variants.FirstOrDefault(v => v.hash == canonical.Id)
                        ?? variants.OrderBy(v => v.hash).First();

                    mappings[canonical.Id] = canonical.Id;

                    foreach (var variant in variants)
                    {
                        mappings[variant.hash] = canonical.Id;
                    }
                }
                else
                {
                    mappings[canonical.Id] = canonical.Id;
                }
            }

            return mappings;
        }
    }
}
