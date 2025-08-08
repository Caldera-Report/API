using Classes.DTO;
using Classes.DestinyApi;

namespace API.Services.Abstract
{
    public interface IDestiny2Service
    {
        public Task<SearchResponse> SearchForPlayer(string playerName);
        public Task<StatisticsResponse> GetStatisticsForPlayer(string membershipId, int membershipType);
        public Task<Dictionary<string, DictionaryComponentResponseOfint64AndDestinyCharacterComponent>> GetCharactersForPlayer(string membershipId, int membershipType);
    }
}
