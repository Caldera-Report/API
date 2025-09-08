using API.Data;
using API.Models.Responses;
using API.Services.Abstract;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace API.Services
{
    public class QueryService : IQueryService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<QueryService> _logger;

        public QueryService(AppDbContext context, ILogger<QueryService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<List<PlayerDto>> GetAllPlayersAsync()
        {
            try
            {
                var players = await _context.Players
                    .Select(PlayerDto.Projection)
                    .ToListAsync();
                return players;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving players from database");
                throw;
            }
        }

        public async Task<List<OpTypeDto>> GetAllActivitiesAsync()
        {
            try
            {
                var activities = await _context.OpTypes
                    .Include(o => o.ActivityTypes)
                    .ThenInclude(at => at.Activities)
                    .Select(OpTypeDto.Projection)
                    .ToListAsync();
                return activities;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving activities from database");
                throw;
            }
        }

        public async Task<PlayerDto> GetPlayerAsync(long id)
        {
            try
            {
                var player = await _context.Players
                    .FirstOrDefaultAsync(p => p.Id == id);
                if (player is null)
                    throw new Exception("Player not found");
                return new PlayerDto(player);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error retrieving player {id} from database");
                throw;
            }
        }

        public async Task<List<ActivityReportDto>> GetPlayerReportsForActivityAsync(long playerId, long activityId)
        {
            try
            {
                var reports = await _context.ActivityReports
                    .Where(ar => ar.PlayerId == playerId && ar.ActivityId == activityId)
                    .Select(ActivityReportDto.Projection)
                    .ToListAsync();
                return reports;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error retrieving activity reports for player {playerId} and activity {activityId}");
                throw;
            }
        }
    }
}
