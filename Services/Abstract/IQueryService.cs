using API.Models.Responses;

namespace API.Services.Abstract
{
    public interface IQueryService
    {
        public Task<List<PlayerDto>> GetAllPlayersAsync();
        public Task<List<OpTypeDto>> GetAllActivitiesAsync();
        public Task<PlayerDto> GetPlayerAsync(long id);
        public Task<List<ActivityReportDto>> GetPlayerReportsForActivityAsync(long playerId, long activityId);
    }
}
