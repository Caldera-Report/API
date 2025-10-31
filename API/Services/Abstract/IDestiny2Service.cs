using API.Models.Responses;
using Domain.DB;
using Domain.DestinyApi;

namespace API.Services.Abstract
{
    public interface IDestiny2Service
    {
        public Task<List<PlayerSearchDto>> SearchForPlayer(string playerName);
        public Task<Dictionary<string, DestinyCharacterComponent>> GetCharactersForPlayer(long membershipId, int membershipType);
        public Task CheckPlayerName(DestinyProfileResponse profile, long id);
        public Task LoadPlayerActivityReports(Player player, DateTime lastPlayedActivityDate, string characterId);
        public Task GroupActivityDuplicates();
    }
}
