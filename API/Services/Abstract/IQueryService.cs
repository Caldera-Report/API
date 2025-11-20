using API.Models.Responses;
using Domain.DB;
using Domain.DTO.Responses;
using Domain.Enums;

namespace API.Services.Abstract
{
    public interface IQueryService
    {
        public Task<List<PlayerSearchDto>> GetAllPlayersAsync();
        public Task<List<Player>> GetAllPlayersFromDb();
        public Task<List<OpTypeDto>> GetAllActivitiesAsync();
        public Task CacheAllActivitiesAsync();
        public Task<PlayerDto> GetPlayerAsync(long id);
        public Task<Player> GetPlayerDbObject(long id);
        public Task<ActivityReportListDTO> GetPlayerReportsForActivityAsync(long playerId, long activityId);
        public Task<List<LeaderboardResponse>> GetLeaderboardAsync(long activityId, LeaderboardTypes type, int count, int offset);
        public Task UpdatePlayerEmblems(Player player, string backgroundEmblemPath, string emblemPath);
        public Task<DateTime> GetPlayerLastPlayedActivityDate(long membershipId);
        public Task<List<PlayerSearchDto>> SearchForPlayer(string query);
        public Task<List<LeaderboardResponse>> GetLeaderboardsForPlayer(string playerName, long activityId, LeaderboardTypes type);
    }
}
