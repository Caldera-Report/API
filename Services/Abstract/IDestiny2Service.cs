using API.Models.Responses;
using Classes.DestinyApi;
using Classes.DTO;

namespace API.Services.Abstract
{
    public interface IDestiny2Service
    {
        public Task<List<PlayerDto>> SearchForPlayer(string playerName);
        public Task<StatisticsResponse> GetStatisticsForPlayer(string membershipId, int membershipType);
        public Task<Dictionary<string, DestinyCharacterComponent>> GetCharactersForPlayer(string membershipId, int membershipType);
        public Task<List<PlayerResponse>> GetAllPlayers();
    }
}
