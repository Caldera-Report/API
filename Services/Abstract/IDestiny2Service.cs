using API.Models.DestinyApi.Character;
using API.Models.Responses;

namespace API.Services.Abstract
{
    public interface IDestiny2Service
    {
        public Task<SearchResponse> SearchForPlayer(string playerName);
        public Task<StatisticsResponse> GetStatisticsForPlayer(string membershipId, int membershipType);
        public Task<Dictionary<string, Character>> GetCharactersForPlayer(string membershipId, int membershipType);
    }
}
