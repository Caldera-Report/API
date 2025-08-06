using API.Clients.Abstract;
using API.Models.Constants;
using API.Models.DestinyApi;
using API.Models.DestinyApi.Activity;
using API.Models.DestinyApi.Character;
using API.Models.Responses;
using API.Services.Abstract;

namespace API.Services
{
    public class Destiny2Service : IDestiny2Service
    {
        private IDestiny2ApiClient _client;
        public Destiny2Service(IDestiny2ApiClient client)
        {
            _client = client;
        }

        public async Task<SearchResponse> SearchForPlayer(string playerName)
        {
            var page = 0;
            var response = await _client.PerformSearch(playerName, page);
            var hasMore = response.Response.HasMore;
            while (hasMore)
            {
                page++;
                var searchResponse = await _client.PerformSearch(playerName, page);
                response.Response.SearchResults.AddRange(searchResponse.Response.SearchResults);
                hasMore = searchResponse.Response.HasMore;
            }

            var filteredMemberships = response.Response.SearchResults
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

            return result;
        }

        public async Task<StatisticsResponse> GetStatisticsForPlayer(string membershipId, int membershipType)
        {
            var characters = await GetCharactersForPlayer(membershipId, membershipType);

            var activityTasks = new List<Task<IEnumerable<Activity>>>();

            async Task<IEnumerable<Activity>> GetRelevantActivityDataForCharacter(int membershipType, string membershipId, string characterId)
            {
                try
                {
                    var activityResponse = await _client.GetActivityAggregateForCharacter(membershipId, membershipType, characterId);
                    return activityResponse.Response.Activities.Where(a => ActivityConstants.AllActivities.Contains(a.ActivityHash));
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
                        .Where(a => a.ActivityHash == ActivityConstants.Caldera && a.Values["fastestCompletionMsForActivity"].Basic.Value != 0)
                        .Sum(a => (int)a.Values["activityCompletions"].Basic.Value),
                CalderaFastestCompletion = activityResults.SelectMany(a => a)
                        .Where(a => a.ActivityHash == ActivityConstants.Caldera && a.Values["fastestCompletionMsForActivity"].Basic.Value != 0)
                        .Min(a => a.Values["fastestCompletionMsForActivity"].Basic.DisplayValue),
                K1Completions = activityResults.SelectMany(a => a)
                        .Where(a => ActivityConstants.K1.Contains(a.ActivityHash) && a.Values["fastestCompletionMsForActivity"].Basic.Value != 0)
                        .Sum(a => (int)a.Values["activityCompletions"].Basic.Value),
                K1FastestCompletion = activityResults.SelectMany(a => a)
                        .Where(a => ActivityConstants.K1.Contains(a.ActivityHash) && a.Values["fastestCompletionMsForActivity"].Basic.Value != 0)
                        .Min(a => a.Values["fastestCompletionMsForActivity"].Basic.DisplayValue),
                KellsFallCompletions = activityResults.SelectMany(a => a)
                        .Where(a => a.ActivityHash == ActivityConstants.KellsFall && a.Values["fastestCompletionMsForActivity"].Basic.Value != 0)
                        .Sum(a => (int)a.Values["activityCompletions"].Basic.Value),
                KellsFallFastestCompletion = activityResults.SelectMany(a => a)
                        .Where(a => a.ActivityHash == ActivityConstants.KellsFall && a.Values["fastestCompletionMsForActivity"].Basic.Value != 0)
                        .Min(a => a.Values["fastestCompletionMsForActivity"].Basic.DisplayValue),
                EncoreCompletions = activityResults.SelectMany(a => a)
                        .Where(a => a.ActivityHash == ActivityConstants.Encore && a.Values["fastestCompletionMsForActivity"].Basic.Value != 0)
                        .Sum(a => (int)a.Values["activityCompletions"].Basic.Value),
                EncoreFastestCompletion = activityResults.SelectMany(a => a)
                        .Where(a => a.ActivityHash == ActivityConstants.Encore && a.Values["fastestCompletionMsForActivity"].Basic.Value != 0)
                        .Min(a => a.Values["fastestCompletionMsForActivity"].Basic.DisplayValue),
            };

            return stats;
        }

        public async Task<Dictionary<string, Character>> GetCharactersForPlayer(string membershipId, int membershipType)
        {
            var characters = await _client.GetCharactersForPlayer(membershipId, membershipType);
            return characters.Response.CharacterData.Data;
        }
    }

}
