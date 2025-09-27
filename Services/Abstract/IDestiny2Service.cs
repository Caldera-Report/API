using API.Models.Responses;
using Classes.DB;
using Classes.DestinyApi;
using Classes.DTO;

namespace API.Services.Abstract
{
    public interface IDestiny2Service
    {
        public Task<List<PlayerDto>> SearchForPlayer(string playerName);
        public Task<Dictionary<string, DestinyCharacterComponent>> GetCharactersForPlayer(long membershipId, int membershipType);
        public Task LoadPlayerActivityReports(Player player, string characterId);
    }
}
