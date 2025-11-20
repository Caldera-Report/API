using API.Domain.DTO.Responses;
using API.Models.Responses;
using API.Services.Abstract;
using Domain.Data;
using Domain.DB;
using Domain.DTO.Responses;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using System.Globalization;
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
                return await _context.Players
                    .Select(PlayerSearchDto.Projection)
                    .ToListAsync();
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
                    .Include(o => o.Activities.Where(a => a.Enabled))
                    .Where(o => o.Activities.Any(a => a.Enabled))
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

        public async Task<List<LeaderboardResponse>> GetLeaderboardAsync(long activityId, LeaderboardTypes type, int count, int offset)
        {
            try
            {
                if (count == 250 && offset == 0)
                {
                    var cachedData = await _cache.StringGetAsync($"leaderboard:activity:{activityId}:type:{(int)type}");
                    if (cachedData.HasValue)
                    {
                        var leaderboardData = JsonSerializer.Deserialize<List<PlayerLeaderboard>>(cachedData!);
                        if (leaderboardData != null)
                        {
                            return leaderboardData
                                .Select(pl => new LeaderboardResponse
                                {
                                    Player = new PlayerDto(pl.Player),
                                    Rank = pl.Rank,
                                    Data = pl.Data?.Completions?.ToString("0,0", CultureInfo.InvariantCulture) ?? pl.Data?.Score?.ToString("0,0", CultureInfo.InvariantCulture) ?? pl.Data?.Duration.ToString() ?? string.Empty
                                })
                                .ToList();
                        }
                    }
                }

                var leaderboard = await _context.PlayerLeaderboards
                    .AsNoTracking()
                    .Include(pl => pl.Player)
                    .Where(pl => pl.ActivityId == activityId && pl.LeaderboardType == type)
                    .OrderBy(pl => pl.Rank)
                    .Skip(offset)
                    .Take(count)
                    .ToListAsync();

                return leaderboard
                    .Select(pl => new LeaderboardResponse()
                    {
                        Player = new PlayerDto(pl.Player),
                        Rank = pl.Rank,
                        Data = pl.Data?.Completions?.ToString("0,0", CultureInfo.InvariantCulture) ?? pl.Data?.Score?.ToString("0,0", CultureInfo.InvariantCulture) ?? pl.Data?.Duration.ToString() ?? string.Empty
                    })
                    .ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving completions leaderboard for activity {ActivityId}", activityId);
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

        public async Task<List<PlayerSearchDto>> SearchForPlayer(string query)
        {
            try
            {
                var results = await _context.Players
                    .Where(p => EF.Functions.ILike(p.DisplayName, $"%{query}%"))
                    .Select(PlayerSearchDto.Projection)
                    .Take(25)
                    .ToListAsync();
                return results;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching for player with query {Query}", query);
                throw;
            }
        }

        public async Task<List<LeaderboardResponse>> GetLeaderboardsForPlayer(string playerName, long activityId, LeaderboardTypes type)
        {
            try
            {
                var leaderboards = await _context.PlayerLeaderboards
                    .AsNoTracking()
                    .Include(pl => pl.Player)
                    .Where(pl => EF.Functions.ILike(pl.FullDisplayName, $"%{playerName}%") && pl.LeaderboardType == type && pl.ActivityId == activityId)
                    .OrderBy(pl => pl.Rank)
                    .Take(250)
                    .ToListAsync();
                return leaderboards
                    .Select(pl => new LeaderboardResponse()
                    {
                        Player = new PlayerDto(pl.Player),
                        Rank = pl.Rank,
                        Data = pl.Data?.Completions?.ToString("0,0", CultureInfo.InvariantCulture) ?? pl.Data?.Score?.ToString("0,0", CultureInfo.InvariantCulture) ?? pl.Data?.Duration.ToString() ?? string.Empty
                    })
                    .ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving leaderboards for player {PlayerName}", playerName);
                throw;
            }
        }
    }
}
