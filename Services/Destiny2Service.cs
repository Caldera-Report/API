using API.Clients.Abstract;
using API.Services.Abstract;
using Classes.DestinyApi;
using Classes.DTO;
using Microsoft.Extensions.Caching.Distributed;
using System.Text.Json;

namespace API.Services
{
    public class Destiny2Service : IDestiny2Service
    {
        private readonly IDestiny2ApiClient _client;
        private readonly IDistributedCache _cache;
        public Destiny2Service(IDestiny2ApiClient client, IDistributedCache cache)
        {
            _client = client;
            _cache = cache; 
        }

        public async Task<SearchResponse> SearchForPlayer(string playerName)
        {
            var cacheKey = $"Search:{playerName}";
            var cached = await _cache.GetStringAsync(cacheKey);
            if (cached != null)
                return JsonSerializer.Deserialize<SearchResponse>(cached);

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
                    });

            var result = new SearchResponse
            {
                Results = filteredMemberships.ToList()
            };

            await _cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(result), new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(14)
            });

            return result;
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

            var response = _client.PerformSearchByBungieName(player, -1);

            var results = response.Result.Response;

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

            return stats;
        }

        public async Task<Dictionary<string, DictionaryComponentResponseOfint64AndDestinyCharacterComponent>> GetCharactersForPlayer(string membershipId, int membershipType)
        {
            var characters = await _client.GetCharactersForPlayer(membershipId, membershipType);
            return characters.Response.characters;
        }
    }

}
