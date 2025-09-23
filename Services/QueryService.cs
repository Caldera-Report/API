using API.Data;
using API.Models.Responses;
using API.Services.Abstract;
using Classes.DB;
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
                    .Include(o => o.Activities)
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

        public async Task<Player> GetPlayerDbObject(long id)
        {
            try
            {
                var player = await _context.Players
                    .Include(p => p.LastActivityReport)
                    .FirstOrDefaultAsync(p => p.Id == id);
                if (player is null)
                    throw new Exception("Player not found");
                return player;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error retrieving player {id} from database");
                throw;
            }
        }

        public async Task<ActivityReportListDTO> GetPlayerReportsForActivityAsync(long playerId, long activityId)
        {
            try
            {
                var reports = await _context.ActivityReports
                    .Where(ar => ar.PlayerId == playerId && ar.ActivityId == activityId)
                    .Select(ActivityReportDto.Projection)
                    .ToListAsync();
                var averageMs = reports.Count > 0 ? reports.Where(r => r.Completed).Select(r => r.Duration.TotalMilliseconds).Average() : 0;
                var average = TimeSpan.FromMilliseconds(averageMs);
                var fastest = reports.OrderBy(r => r.Duration).FirstOrDefault(r => r.Completed);
                return new ActivityReportListDTO
                {
                    Reports = reports,
                    Average = average,
                    Best = fastest
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error retrieving activity reports for player {playerId} and activity {activityId}");
                throw;
            }
        }

        public async Task<List<CompletionsLeaderboardResponse>> GetCompletionsLeaderboardAsync(long activityId)
        {
            try
            {
                var query = _context.ActivityReports
                    .AsNoTracking()
                    .Where(ar => ar.Completed);

                if (activityId > 0)
                    query = query.Where(ar => ar.ActivityId == activityId);

                var leaderboard = await query
                    .GroupBy(ar => ar.PlayerId)
                    .Select(g => new
                    {
                        PlayerId = g.Key,
                        Completions = g.Count()
                    })
                    .OrderByDescending(x => x.Completions)
                    .ThenBy(x => x.PlayerId)
                    .Join(
                        _context.Players.AsNoTracking(),
                        g => g.PlayerId,
                        p => p.Id,
                        (g, p) => new CompletionsLeaderboardResponse
                        {
                            Player = new PlayerDto(p),
                            Completions = g.Completions
                        })
                    .ToListAsync();

                return leaderboard;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving completions leaderboard for activity {ActivityId}", activityId);
                throw;
            }
        }

        public async Task<List<TimeLeaderboardResponse>> GetSpeedLeaderboardAsync(long activityId)
        {
            try
            {
                var query = _context.ActivityReports
                    .AsNoTracking()
                    .Where(ar => ar.Completed);
                if (activityId > 0)
                    query = query.Where(ar => ar.ActivityId == activityId);
                var leaderboard = await query
                    .GroupBy(ar => ar.PlayerId)
                    .Select(g => new
                    {
                        PlayerId = g.Key,
                        BestTime = g.Min(ar => ar.Duration)
                    })
                    .OrderBy(x => x.BestTime)
                    .ThenBy(x => x.PlayerId)
                    .Join(
                        _context.Players.AsNoTracking(),
                        g => g.PlayerId,
                        p => p.Id,
                        (g, p) => new TimeLeaderboardResponse
                        {
                            Player = new PlayerDto(p),
                            Time = g.BestTime
                        })
                    .ToListAsync();
                return leaderboard;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving speed leaderboard for activity {ActivityId}", activityId);
                throw;
            }
        }

        public async Task<List<TimeLeaderboardResponse>> GetTotalTimeLeaderboardAsync(long activityId)
        {
            try
            {
                var query = _context.ActivityReports
                    .AsNoTracking()
                    .Where(ar => ar.Duration > TimeSpan.FromSeconds(0));
                if (activityId > 0)
                    query = query.Where(ar => ar.ActivityId == activityId);
                var leaderboard = await query
                    .GroupBy(ar => ar.PlayerId)
                    .Select(g => new
                    {
                        PlayerId = g.Key,
                        TotalTime = g.Sum(ar => ar.Duration.Seconds)
                    })
                    .OrderByDescending(x => x.TotalTime)
                    .ThenBy(x => x.PlayerId)
                    .Join(
                        _context.Players.AsNoTracking(),
                        g => g.PlayerId,
                        p => p.Id,
                        (g, p) => new TimeLeaderboardResponse
                        {
                            Player = new PlayerDto(p),
                            Time = TimeSpan.FromSeconds(g.TotalTime)
                        })
                    .ToListAsync();
                return leaderboard;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving total time leaderboard for activity {ActivityId}", activityId);
                throw;
            }
        }

        public async Task UpdatePlayerEmblems(Player player, string backgroundEmblemPath, string emblemPath)
        {
            try
            {
                player.LastPlayedCharacterEmblemPath = emblemPath;
                player.LastPlayedCharacterBackgroundPath = backgroundEmblemPath;
                _context.Players.Update(player);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating emblem for player {player.Id}");
                throw;
            }
        }
    }
}
