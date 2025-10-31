using API.Domain.DTO.Responses;
using API.Models.Responses;
using API.Services.Abstract;
using Domain.Data;
using Domain.DB;
using Domain.DTO.Responses;
using Facet.Extensions.EFCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using System.Text.Json;

namespace API.Services
{
    public class QueryService : IQueryService
    {
        private readonly AppDbContext _context;
        private readonly IDatabase _cache;
        private readonly ILogger<QueryService> _logger;

        public QueryService(AppDbContext context, IConnectionMultiplexer redis, ILogger<QueryService> logger)
        {
            _context = context;
            _cache = redis.GetDatabase();
            _logger = logger;
        }

        public async Task<List<PlayerSearchDto>> GetAllPlayersAsync()
        {
            try
            {
                var players = await _cache.StringGetAsync("players:all");
                if (players.HasValue)
                    return JsonSerializer.Deserialize<List<PlayerSearchDto>>(players!)!;
                else
                {
                    var playerList = await _context.Players
                        .ToFacetsAsync<PlayerSearchDto>();
                    await _cache.StringSetAsync("players:all", JsonSerializer.SerializeToUtf8Bytes(playerList), new TimeSpan(1, 0, 0));
                    return playerList;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving players from database");
                throw;
            }
        }

        public async Task<List<Player>> GetAllPlayersFromDb()
        {
            try
            {
                return await _context.Players
                    .ToListAsync();
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
                var activities = await _cache.StringGetAsync("activities:all");
                if (activities.HasValue)
                {
                    return JsonSerializer.Deserialize<List<OpTypeDto>>(activities)
                        ?? new List<OpTypeDto>();
                }
                else
                {
                    await CacheAllActivitiesAsync();
                    activities = await _cache.StringGetAsync("activities:all");
                    if (activities.HasValue)
                    {
                        return JsonSerializer.Deserialize<List<OpTypeDto>>(activities)
                            ?? new List<OpTypeDto>();
                    }
                    else
                    {
                        return new List<OpTypeDto>();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving activities from database");
                throw;
            }
        }

        public async Task CacheAllActivitiesAsync()
        {
            try
            {
                var activities = await _context.OpTypes
                    .Include(o => o.Activities)
                    .Select(OpTypeDto.Projection)
                    .ToListAsync();
                await _cache.StringSetAsync("activities:all", JsonSerializer.SerializeToUtf8Bytes(activities), new TimeSpan(1, 1, 0, 0));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error caching activities");
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
                var reports = await _context.ActivityReportPlayers
                    .Include(arp => arp.ActivityReport)
                    .Where(arp => arp.ActivityReport.ActivityId == activityId && arp.PlayerId == playerId)
                    .Select(ActivityReportPlayerFacet.Projection)
                    .ToListAsync();
                var averageMs = reports.Count(r => r.Completed) > 0 ? reports.Where(r => r.Completed).Select(r => r.Duration.TotalMilliseconds).Average() : 0;
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
                var cachedData = await _cache.StringGetAsync($"leaderboard:completions:{activityId}");
                if (cachedData.HasValue)
                {
                    return JsonSerializer.Deserialize<List<CompletionsLeaderboardResponse>>(cachedData)
                        ?? new List<CompletionsLeaderboardResponse>();
                }
                else
                {
                    await ComputeCompletionsLeaderboardAsync(activityId);
                    cachedData = await _cache.StringGetAsync($"leaderboard:completions:{activityId}");
                    if (cachedData.HasValue)
                    {
                        return JsonSerializer.Deserialize<List<CompletionsLeaderboardResponse>>(cachedData)
                            ?? new List<CompletionsLeaderboardResponse>();
                    }
                    else
                    {
                        return new List<CompletionsLeaderboardResponse>();
                    }
                }
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
                var cachedData = await _cache.StringGetAsync($"leaderboard:speed:{activityId}");
                if (cachedData.HasValue)
                {
                    return JsonSerializer.Deserialize<List<TimeLeaderboardResponse>>(cachedData)
                        ?? new List<TimeLeaderboardResponse>();
                }
                else
                {
                    await ComputeSpeedLeaderboardAsync(activityId);
                    cachedData = await _cache.StringGetAsync($"leaderboard:speed:{activityId}");
                    if (cachedData.HasValue)
                    {
                        return JsonSerializer.Deserialize<List<TimeLeaderboardResponse>>(cachedData)
                            ?? new List<TimeLeaderboardResponse>();
                    }
                    else
                    {
                        return new List<TimeLeaderboardResponse>();
                    }
                }
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
                var cachedData = await _cache.StringGetAsync($"leaderboard:totalTime:{activityId}");
                if (cachedData.HasValue)
                {
                    return JsonSerializer.Deserialize<List<TimeLeaderboardResponse>>(cachedData)
                        ?? new List<TimeLeaderboardResponse>();
                }
                else
                {
                    await ComputeTotalTimeLeaderboardAsync(activityId);
                    cachedData = await _cache.StringGetAsync($"leaderboard:totalTime:{activityId}");
                    if (cachedData.HasValue)
                    {
                        return JsonSerializer.Deserialize<List<TimeLeaderboardResponse>>(cachedData)
                            ?? new List<TimeLeaderboardResponse>();
                    }
                    else
                    {
                        return new List<TimeLeaderboardResponse>();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving total time leaderboard for activity {ActivityId}", activityId);
                throw;
            }
        }

        public async Task ComputeCompletionsLeaderboardAsync(long activityId)
        {
            try
            {
                var query = _context.ActivityReportPlayers
                    .AsNoTracking()
                    .Include(ar => ar.Player)
                    .Include(ar => ar.ActivityReport)
                    .Where(ar => ar.Completed);

                if (activityId > 0)
                    query = query.Where(ar => ar.ActivityReport.ActivityId == activityId);

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

                await _cache.StringSetAsync($"leaderboard:completions:{activityId}", JsonSerializer.SerializeToUtf8Bytes(leaderboard), new TimeSpan(1, 1, 0, 0));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving completions leaderboard for activity {ActivityId}", activityId);
                throw;
            }
        }

        public async Task ComputeSpeedLeaderboardAsync(long activityId)
        {
            try
            {
                var query = _context.ActivityReportPlayers
                    .AsNoTracking()
                    .Include(ar => ar.ActivityReport)
                    .Where(ar => ar.Completed);
                if (activityId > 0)
                    query = query.Where(ar => ar.ActivityReport.ActivityId == activityId);
                var leaderboard = await query
                    .GroupBy(ar => ar.PlayerId)
                    .Select(g => new
                    {
                        PlayerId = g.Key,
                        BestTime = g.Min(ar => ar.Duration),
                        CompletedDate = g.OrderBy(ar => ar.Duration).Select(ar => ar.ActivityReport.Date).First()
                    })
                    .OrderBy(x => x.BestTime)
                    .ThenBy(x => x.CompletedDate)
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
                await _cache.StringSetAsync($"leaderboard:speed:{activityId}", JsonSerializer.SerializeToUtf8Bytes(leaderboard), new TimeSpan(1, 1, 0, 0));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving speed leaderboard for activity {ActivityId}", activityId);
                throw;
            }
        }

        public async Task ComputeTotalTimeLeaderboardAsync(long activityId)
        {
            try
            {
                var query = _context.ActivityReportPlayers
                    .AsNoTracking()
                    .Include(ar => ar.ActivityReport)
                    .Where(ar => ar.Duration > TimeSpan.FromSeconds(0));
                if (activityId > 0)
                    query = query.Where(ar => ar.ActivityReport.ActivityId == activityId);
                var leaderboard = await query
                    .GroupBy(ar => ar.PlayerId)
                    .Select(g => new
                    {
                        PlayerId = g.Key,
                        TotalTime = g.Sum(ar => ar.Duration.TotalSeconds)
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
                await _cache.StringSetAsync($"leaderboard:totalTime:{activityId}", JsonSerializer.SerializeToUtf8Bytes(leaderboard), new TimeSpan(1, 1, 0, 0));
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

        public async Task<DateTime> GetPlayerLastPlayedActivityDate(long membershipId)
        {
            var lastActivity = await _context.ActivityReports
                .AsNoTracking()
                .Include(r => r.Players)
                .Where(r => r.Players.Any(p => p.PlayerId == membershipId) && !r.NeedsFullCheck)
                .OrderByDescending(r => r.Date)
                .Select(r => (DateTime?)r.Date)
                .FirstOrDefaultAsync();
            return lastActivity ?? new DateTime(2025, 7, 15);
        }

        public async Task LoadPlayersQueue()
        {
            var playerIds = await _context.Players
                .AsNoTracking()
                .Select(p => p.Id)
                .ToArrayAsync();

            await _cache.ListRightPushAsync("player-crawl-queue", playerIds.Select(id => (RedisValue)id).ToArray());
        }
    }
}
