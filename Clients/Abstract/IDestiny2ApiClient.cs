using API.Models.DestinyApi;
using API.Models.DestinyApi.Activity;
using API.Models.DestinyApi.Character;
using API.Models.DestinyApi.Search;

namespace API.Clients.Abstract
{
    public interface IDestiny2ApiClient
    {
        public Task<DestinyApiResponse<SearchResponse>> PerformSearch(string playerName);
        public Task<DestinyApiResponse<CharacterResponse>> GetCharactersForPlayer(string membershipId, int membershipType);
        public Task<DestinyApiResponse<ActivityResponse>> GetActivityAggregateForCharacter(string membershipId, int membershipType, string characterId);
    }
}
