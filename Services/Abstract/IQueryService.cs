using API.Models.Responses;
using Classes.DB;

namespace API.Services.Abstract
{
    public interface IQueryService
    {
        public Task<List<PlayerSearchDto>> GetAllPlayersAsync();
        public Task<List<OpTypeDto>> GetAllActivitiesAsync();
        public Task CacheAllActivitiesAsync();
        public Task<PlayerDto> GetPlayerAsync(long id);
        public Task<Player> GetPlayerDbObject(long id);
        public Task<ActivityReportListDTO> GetPlayerReportsForActivityAsync(long playerId, long activityId);
        public Task<List<CompletionsLeaderboardResponse>> GetCompletionsLeaderboardAsync(long activityId);
        public Task<List<TimeLeaderboardResponse>> GetSpeedLeaderboardAsync(long activityId);
        public Task<List<TimeLeaderboardResponse>> GetTotalTimeLeaderboardAsync(long activityId);
        public Task ComputeCompletionsLeaderboardAsync(long activityId);
        public Task ComputeSpeedLeaderboardAsync(long activityId);
        public Task ComputeTotalTimeLeaderboardAsync(long activityId);
        public Task UpdatePlayerEmblems(Player player, string backgroundEmblemPath, string emblemPath);
    }
}
