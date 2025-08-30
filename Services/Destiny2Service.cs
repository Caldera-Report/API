using API.Clients.Abstract;
using API.Data;
using API.Services.Abstract;
using Classes.DB;
using Classes.DestinyApi;
using Classes.DTO;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using System.Text.Json;

namespace API.Services
{
    public class Destiny2Service : IDestiny2Service
    {
        private readonly IDestiny2ApiClient _client;
        private readonly IDistributedCache _cache;
        private readonly AppDbContext _context;
        public Destiny2Service(IDestiny2ApiClient client, IDistributedCache cache, AppDbContext context)
        {
            _client = client;
            _cache = cache; 
            _context = context;
        }

        public async Task<SearchResponse> SearchForPlayer(string playerName)
        {
            var hasBungieId = playerName.Length > 5 && playerName[^5] == '#';

            var response = hasBungieId ? 
                await SearchByBungieName(playerName) :
                await SearchByPrefix(playerName);

            var filteredMemberships = response
                    .Where(m => m.applicableMembershipTypes.Count > 0)
                    .Select(m => new SearchResult
                    { 
                        DestinyMembershipId = m.membershipId.ToString(),
                        MembershipType = m.membershipType,
                        DisplayName = m.bungieGlobalDisplayName,
                        DisplayNameCode = m.bungieGlobalDisplayNameCode
                    })
                    .DistinctBy(r => r.DestinyMembershipId); //because apparently sometimes there are dupes

            var result = new SearchResponse
            {
                Results = filteredMemberships.ToList()
            };

            await AddSearchResultsToDb(result);

            return result;
        }

        public async Task AddSearchResultsToDb(SearchResponse result)
        {
            var membershipIds = result.Results
                .Select(m => long.Parse(m.DestinyMembershipId))
                .ToList();
            var membershipTypes = result.Results
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
            foreach (var membership in result.Results.Distinct())
            {
                var id = long.Parse(membership.DestinyMembershipId);
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

        public async Task<StatisticsResponse> GetStatisticsForPlayer(string membershipId, int membershipType)
        {
            var cacheKey = $"GetStats:{membershipType}+{membershipId}";
            var cached = await _cache.GetStringAsync(cacheKey);
            if (cached != null)
                return JsonSerializer.Deserialize<StatisticsResponse>(cached);

            var characters = await GetCharactersForPlayer(membershipId, membershipType);

            var activityTasks = new List<Task<IEnumerable<DestinyAggregateActivityStats>>>();

            async Task<IEnumerable<DestinyAggregateActivityStats>> GetRelevantActivityDataForCharacter(int membershipType, string membershipId, string characterId)
            {
                try
                {
                    var activityResponse = await _client.GetActivityAggregateForCharacter(membershipId, membershipType, characterId);
                    if (activityResponse.Response.activities == null)
                        return Enumerable.Empty<DestinyAggregateActivityStats>();
                    return activityResponse.Response.activities.Where(a => DestinyApiConstants.AllActivities.Contains(a.activityHash));
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException("An error occurred while fetching activity data for the character.", ex);
                }
            }

            foreach (var character in characters)
            {
                activityTasks.Add(GetRelevantActivityDataForCharacter(membershipType, membershipId, character.Key));
            }

            var activityResults = await Task.WhenAll(activityTasks);

            var stats = new StatisticsResponse
            {
                CalderaCompletions = activityResults.SelectMany(a => a)
                        .Where(a => a.activityHash == DestinyApiConstants.Caldera && a.values["fastestCompletionMsForActivity"].basic.value != 0)
                        .Sum(a => (int)a.values["activityCompletions"].basic.value),
                CalderaFastestCompletion = activityResults.SelectMany(a => a)
                        .Where(a => a.activityHash == DestinyApiConstants.Caldera && a.values["fastestCompletionMsForActivity"].basic.value != 0)
                        .Min(a => a.values["fastestCompletionMsForActivity"].basic.displayValue),
                K1Completions = activityResults.SelectMany(a => a)
                        .Where(a => DestinyApiConstants.K1.Contains(a.activityHash) && a.values["fastestCompletionMsForActivity"].basic.value != 0)
                        .Sum(a => (int)a.values["activityCompletions"].basic.value),
                K1FastestCompletion = activityResults.SelectMany(a => a)
                        .Where(a => DestinyApiConstants.K1.Contains(a.activityHash) && a.values["fastestCompletionMsForActivity"].basic.value != 0)
                        .Min(a => a.values["fastestCompletionMsForActivity"].basic.displayValue),
                KellsFallCompletions = activityResults.SelectMany(a => a)
                        .Where(a => a.activityHash == DestinyApiConstants.KellsFall && a.values["fastestCompletionMsForActivity"].basic.value != 0)
                        .Sum(a => (int)a.values["activityCompletions"].basic.value),
                KellsFallFastestCompletion = activityResults.SelectMany(a => a)
                        .Where(a => a.activityHash == DestinyApiConstants.KellsFall && a.values["fastestCompletionMsForActivity"].basic.value != 0)
                        .Min(a => a.values["fastestCompletionMsForActivity"].basic.displayValue),
                EncoreCompletions = activityResults.SelectMany(a => a)
                        .Where(a => a.activityHash == DestinyApiConstants.Encore && a.values["fastestCompletionMsForActivity"].basic.value != 0)
                        .Sum(a => (int)a.values["activityCompletions"].basic.value),
                EncoreFastestCompletion = activityResults.SelectMany(a => a)
                        .Where(a => a.activityHash == DestinyApiConstants.Encore && a.values["fastestCompletionMsForActivity"].basic.value != 0)
                        .Min(a => a.values["fastestCompletionMsForActivity"].basic.displayValue),
            };

            await _cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(stats), new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)
            });

            var player = await _context.Players.FirstOrDefaultAsync(p => p.Id == long.Parse(membershipId));

            if (player != null)
            {
                player.LastProfileView = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }

            return stats;
        }

        public async Task<Dictionary<string, DestinyCharacterComponent>> GetCharactersForPlayer(string membershipId, int membershipType)
        {
            var characters = await _client.GetCharactersForPlayer(membershipId, membershipType);
            return characters.Response.characters.data;
        }

        public async Task<List<PlayerResponse>> GetAllPlayers()
        {
            var players = _context.Players.Select(
                players => new PlayerResponse
                {
                    Id = players.Id.ToString(),
                    MembershipType = players.MembershipType,
                    DisplayName = players.DisplayName,
                    DisplayNameCode = players.DisplayNameCode,
                    LastUpdateStatus = "Complete",
                    UpdatePriority = 1000000,
                    LastProfileView = players.LastProfileView
                }
            )
            .ToList();
            if (players.Count == 0)
                return null;
            return players;
        }
    }

}
