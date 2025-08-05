using API.Clients.Abstract;
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
            var D2response = await _client.PerformSearch(playerName);

            var result = new SearchResponse
            {
                Results = D2response.Response.SearchResults.Select(r => new SearchResult
                {
                    DisplayName = r.BungieGlobalDisplayName,
                    DisplayNameCode = r.BungieGlobalDisplayNameCode,
                    DestinyMembershipId = r.DestinyMemberships.First().MembershipId,
                    MembershipType = r.DestinyMemberships.First().MembershipType
                }).ToList()
            };

            return result;
        }
    }
}
