using API.Clients.Abstract;
using API.Data;
using API.Models.Responses;
using API.Services.Abstract;
using Classes.DB;
using Classes.DestinyApi;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;

namespace API.Services
{
    public class Destiny2Service : IDestiny2Service
    {
        private readonly IDestiny2ApiClient _client;
        private readonly IDistributedCache _cache;
        private readonly AppDbContext _context;

        private static readonly DateTime ActivityCutoffUtc = new(2025, 7, 15, 0, 0, 0, DateTimeKind.Utc);
        public Destiny2Service(IDestiny2ApiClient client, IDistributedCache cache, AppDbContext context)
        {
            _client = client;
            _cache = cache; 
            _context = context;
        }

        public async Task<List<PlayerDto>> SearchForPlayer(string playerName)
        {
            var hasBungieId = playerName.Length > 5 && playerName[^5] == '#';

            var response = hasBungieId ? 
                await SearchByBungieName(playerName) :
                await SearchByPrefix(playerName);

            var filteredMemberships = response
                    .Where(m => m.applicableMembershipTypes.Count > 0)
                    .Select(m => new PlayerDto
                    { 
                        Id = long.Parse(m.membershipId),
                        MembershipType = m.membershipType,
                        DisplayName = m.bungieGlobalDisplayName,
                        DisplayNameCode = m.bungieGlobalDisplayNameCode,
                        FullDisplayName = m.bungieGlobalDisplayName + "#" + m.bungieGlobalDisplayNameCode
                    })
                    .DistinctBy(r => r.Id)
                    .ToList(); //because apparently sometimes there are dupes

            await AddSearchResultsToDb(filteredMemberships);

            return filteredMemberships;
        }

        public async Task AddSearchResultsToDb(List<PlayerDto> result)
        {
            var membershipIds = result
                .Select(m => m.Id)
                .ToList();
            var membershipTypes = result
                .Select(m => m.MembershipType)
                .ToList();

            var existingPlayers = _context.Players
                .Where(p => membershipIds.Contains(p.Id) && membershipTypes.Contains(p.MembershipType))
                .Select(p => new { p.Id, p.MembershipType })
                .ToList();

            var existingPlayerKeys = new HashSet<(long, int)>(
                existingPlayers.Select(p => (p.Id, p.MembershipType))
            );

            var newPlayers = new List<Player>();
            foreach (var membership in result.Distinct())
            {
                var id = membership.Id;
                var type = membership.MembershipType;
                if (!existingPlayerKeys.Contains((id, type)))
                {
                    newPlayers.Add(new Player
                    {
                        Id = id,
                        MembershipType = type,
                        DisplayName = membership.DisplayName,
                        DisplayNameCode = membership.DisplayNameCode,
                        UpdatePriority = 1000000,
                        LastUpdateStatus = "Completed"
                    });
                }
            }

            if (newPlayers.Count > 0)
            {
                _context.Players.AddRange(newPlayers);
            }

            await _context.SaveChangesAsync();
        }

        public async Task<IEnumerable<UserInfoCard>> SearchByBungieName(string playerName)
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

        public async Task<IEnumerable<UserInfoCard>> SearchByPrefix(string playerName)
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
            var characters = await _client.GetCharactersForPlayer(membershipId, membershipType);
            return characters.Response.characters.data;
        }

        public async Task LoadPlayerActivityReports(Player player, string characterId)
        {
            var activityHashMap = await _context.ActivityHashMappings.AsNoTracking()
                .ToDictionaryAsync(m => m.SourceHash, m => m.CanonicalActivityId);

            player.LastUpdateStatus = "Updating";
            player.LastUpdateStarted = DateTime.UtcNow;
            _context.Players.Update(player);
            await _context.SaveChangesAsync();

            var page = 0;
            var reportsToAdd = new List<ActivityReport>();
            var hasReachedLastUpdate = false;

            var lastPlayed = player.LastPlayed ?? DateTime.MinValue;

            var since = DateTime.UtcNow - lastPlayed;
            var activityCount = since < TimeSpan.FromMinutes(15) ? 10 :
                since < TimeSpan.FromHours(2) ? 25 :
                since < TimeSpan.FromHours(24) ? 50 :
                since < TimeSpan.FromDays(7) ? 100 :
                250;

            Task<DestinyApiResponse<DestinyActivityHistoryResults>> inFlight =
            _client.GetHistoricalStatsForCharacter(player.Id, player.MembershipType, characterId, page, activityCount);

            try
            {
                while (!hasReachedLastUpdate)
                {
                    var response = await inFlight;
                    if (response.ErrorCode != 1 || response.Response?.activities == null || !response.Response.activities.Any())
                        break;

                    page++;
                    var prefetchNext = _client.GetHistoricalStatsForCharacter(player.Id, player.MembershipType, characterId, page, activityCount);

                    foreach (var activity in response.Response.activities)
                    {
                        hasReachedLastUpdate = activity.period <= (player.LastActivityReport?.Date ?? DateTime.MinValue);
                        if (activity.period < ActivityCutoffUtc || hasReachedLastUpdate)
                            break;

                        var rawHash = activity.activityDetails.referenceId;
                        if (!activityHashMap.TryGetValue(rawHash, out var canonicalId))
                            continue;

                        if (!long.TryParse(activity.activityDetails.instanceId, out var instanceId))
                            continue;

                        reportsToAdd.Add(new ActivityReport
                        {
                            InstanceId = instanceId,
                            ActivityId = canonicalId,
                            PlayerId = player.Id,
                            Date = activity.period,
                            Completed = activity.values["completed"].basic.value == 1.0,
                            Duration = TimeSpan.FromSeconds(activity.values["activityDurationSeconds"].basic.value),
                        });
                    }

                    if (response.Response.activities.Last().period < ActivityCutoffUtc)
                        break;

                    inFlight = prefetchNext;
                }

                if (reportsToAdd.Any())
                {
                    _context.ActivityReports.AddRange(reportsToAdd);
                    await _context.SaveChangesAsync();
                    player.LastPlayedActivityId = reportsToAdd
                        .OrderByDescending(r => r.Date)
                        .Select(r => r.Id)
                        .FirstOrDefault();
                }

                player.LastUpdateStatus = "Complete";
                player.LastUpdateCompleted = DateTime.UtcNow;
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
    }
}
