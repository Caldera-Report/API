using API.Clients.Abstract;
using API.Models.Constants;
using API.Models.DestinyApi;
using API.Models.DestinyApi.Activity;
using API.Models.DestinyApi.Character;
using API.Models.Responses;
using API.Services.Abstract;
using Microsoft.Extensions.Caching.Distributed;
using System.Text.Json;
using DestinyMembership = API.Models.DestinyApi.Search.DestinyMembership;
using DestinySearchResult = API.Models.DestinyApi.Search.SearchResult;
using SearchByBungieNameResult = API.Models.DestinyApi.Search.SearchByBungieNameResult;
using SearchByPrefix = API.Models.DestinyApi.Search.SearchByPrefix;
using SearchPlayerByName = API.Models.DestinyApi.Search.SearchPlayerByName;

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
                .SelectMany(r => r.DestinyMemberships
                    .Where(m => m.ApplicableMembershipTypes != null && m.ApplicableMembershipTypes.Contains(m.MembershipType))
                    .Select(m => new
                    {
                        Membership = m,
                        r.BungieGlobalDisplayName,
                        r.BungieGlobalDisplayNameCode
                    }))
                .ToList();

            var grouped = filteredMemberships
                .GroupBy(x => x.Membership.MembershipId)
                .Select(g =>
                {
                    var first = g.First();
                    return new SearchResult
                    {
                        DisplayName = first.BungieGlobalDisplayName,
                        DisplayNameCode = first.BungieGlobalDisplayNameCode,
                        DestinyMembershipId = first.Membership.MembershipId,
                        MembershipType = first.Membership.MembershipType
                    };
                })
                .ToList();

            var result = new SearchResponse
            {
                Results = grouped
            };

            await _cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(result), new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(14)
            });

            return result;
        }

        public async Task<IEnumerable<DestinySearchResult>> SearchByBungieName(string playerName)
        {
            var bungieId = int.Parse(playerName[^4..]);
            playerName = playerName[..^5];

            var player = new SearchPlayerByName
            {
                DisplayName = playerName,
                DisplayNameCode = bungieId
            };

            var tasks = new List<Task<DestinyApiResponse<List<SearchByBungieNameResult>>>>();
            foreach (var membershipTypeId in DestinyConstants.MembershipTypeIds)
            {
                tasks.Add(_client.PerformSearchByBungieName(player, membershipTypeId));
            }

            var responses = await Task.WhenAll(tasks);

            var bungieNameResults = responses.Where(r => r.Response.Count() > 0)
                .Select(r => r.Response.First())
                .Where(r => r.applicableMembershipTypes.Count() > 0);
            var results = bungieNameResults
                .Select(r => new DestinySearchResult
                {
                    BungieGlobalDisplayName = r.bungieGlobalDisplayName,
                    BungieGlobalDisplayNameCode = r.bungieGlobalDisplayNameCode,
                    DestinyMemberships = new List<DestinyMembership>
                    {
                        new DestinyMembership
                        {
                            ApplicableMembershipTypes = r.applicableMembershipTypes.ToList(),
                            IsPublic = r.isPublic,
                            MembershipType = r.membershipType,
                            MembershipId = r.membershipId
                        }
                    }
                }).ToList();

            return results;
        }

        public async Task<IEnumerable<DestinySearchResult>> SearchByPrefix(string playerName)
        {
            var page = 0;
            var player = new SearchByPrefix
            {
                DisplayNamePrefix = playerName
            };
            var response = await _client.PerformSearchByPrefix(player, page);
            var hasMore = response.Response.HasMore;
            while (hasMore)
            {
                page++;
                var nextResponse = await _client.PerformSearchByPrefix(player, page);
                response.Response.SearchResults.AddRange(nextResponse.Response.SearchResults);
            }
            return response.Response.SearchResults;
        }

        public async Task<StatisticsResponse> GetStatisticsForPlayer(string membershipId, int membershipType)
        {
            var cacheKey = $"GetStats:{membershipType}+{membershipId}";
            var cached = await _cache.GetStringAsync(cacheKey);
            if (cached != null)
                return JsonSerializer.Deserialize<StatisticsResponse>(cached);

            var characters = await GetCharactersForPlayer(membershipId, membershipType);

            var activityTasks = new List<Task<IEnumerable<Activity>>>();

            async Task<IEnumerable<Activity>> GetRelevantActivityDataForCharacter(int membershipType, string membershipId, string characterId)
            {
                try
                {
                    var activityResponse = await _client.GetActivityAggregateForCharacter(membershipId, membershipType, characterId);
                    return activityResponse.Response.Activities.Where(a => DestinyConstants.AllActivities.Contains(a.ActivityHash));
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
                        .Where(a => a.ActivityHash == DestinyConstants.Caldera && a.Values["fastestCompletionMsForActivity"].Basic.Value != 0)
                        .Sum(a => (int)a.Values["activityCompletions"].Basic.Value),
                CalderaFastestCompletion = activityResults.SelectMany(a => a)
                        .Where(a => a.ActivityHash == DestinyConstants.Caldera && a.Values["fastestCompletionMsForActivity"].Basic.Value != 0)
                        .Min(a => a.Values["fastestCompletionMsForActivity"].Basic.DisplayValue),
                K1Completions = activityResults.SelectMany(a => a)
                        .Where(a => DestinyConstants.K1.Contains(a.ActivityHash) && a.Values["fastestCompletionMsForActivity"].Basic.Value != 0)
                        .Sum(a => (int)a.Values["activityCompletions"].Basic.Value),
                K1FastestCompletion = activityResults.SelectMany(a => a)
                        .Where(a => DestinyConstants.K1.Contains(a.ActivityHash) && a.Values["fastestCompletionMsForActivity"].Basic.Value != 0)
                        .Min(a => a.Values["fastestCompletionMsForActivity"].Basic.DisplayValue),
                KellsFallCompletions = activityResults.SelectMany(a => a)
                        .Where(a => a.ActivityHash == DestinyConstants.KellsFall && a.Values["fastestCompletionMsForActivity"].Basic.Value != 0)
                        .Sum(a => (int)a.Values["activityCompletions"].Basic.Value),
                KellsFallFastestCompletion = activityResults.SelectMany(a => a)
                        .Where(a => a.ActivityHash == DestinyConstants.KellsFall && a.Values["fastestCompletionMsForActivity"].Basic.Value != 0)
                        .Min(a => a.Values["fastestCompletionMsForActivity"].Basic.DisplayValue),
                EncoreCompletions = activityResults.SelectMany(a => a)
                        .Where(a => a.ActivityHash == DestinyConstants.Encore && a.Values["fastestCompletionMsForActivity"].Basic.Value != 0)
                        .Sum(a => (int)a.Values["activityCompletions"].Basic.Value),
                EncoreFastestCompletion = activityResults.SelectMany(a => a)
                        .Where(a => a.ActivityHash == DestinyConstants.Encore && a.Values["fastestCompletionMsForActivity"].Basic.Value != 0)
                        .Min(a => a.Values["fastestCompletionMsForActivity"].Basic.DisplayValue),
            };

            await _cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(stats), new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)
            });

            return stats;
        }

        public async Task<Dictionary<string, Character>> GetCharactersForPlayer(string membershipId, int membershipType)
        {
            var characters = await _client.GetCharactersForPlayer(membershipId, membershipType);
            return characters.Response.CharacterData.Data;
        }
    }

}
