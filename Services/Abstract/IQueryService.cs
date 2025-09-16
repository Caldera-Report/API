using API.Models.Responses;
using Classes.DB;

namespace API.Services.Abstract
{
    public interface IQueryService
    {
        public Task<List<PlayerDto>> GetAllPlayersAsync();
        public Task<List<OpTypeDto>> GetAllActivitiesAsync();
        public Task<PlayerDto> GetPlayerAsync(long id);
        public Task<Player> GetPlayerDbObject(long id);
        public Task<List<ActivityReportDto>> GetPlayerReportsForActivityAsync(long playerId, long activityId);
        public Task<List<CompletionsLeaderboardResponse>> GetCompletionsLeaderboardAsync(long activityId);
        public Task<List<TimeLeaderboardResponse>> GetSpeedLeaderboardAsync(long activityId);
        public Task<List<TimeLeaderboardResponse>> GetTotalTimeLeaderboardAsync(long activityId);
    }
}
