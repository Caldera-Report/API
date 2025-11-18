using Crawler.Services.Abstract;
using Domain.Data;
using Domain.DB;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using StackExchange.Redis;

namespace Crawler.Services
{
    public class LeaderboardService : ILeaderboardService
    {
        private readonly IDbContextFactory<AppDbContext> _contextFactory;
        private readonly IDatabase _cache;
        public LeaderboardService(IDbContextFactory<AppDbContext> contextFactory, IConnectionMultiplexer redis)
        {
            _contextFactory = contextFactory;
            _cache = redis.GetDatabase();
        }

        public async Task ComputeLeaderboards(CancellationToken ct)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(ct);

            var activities = await context.Activities.Where(a => a.Enabled).AsNoTracking().ToListAsync(ct);

            await ComputeDurationLeaderboards(context, activities, ct);
            await ComputeTotalCompletionsLeaderboards(context, activities, ct);
            await ComputeBestScoreLeaderboards(context, activities, ct);

            foreach (var leaderboardType in Enum.GetValues<LeaderboardTypes>())
            {
                foreach (var activityId in await context.Activities.AsNoTracking().Select(a => a.Id).ToListAsync(ct))
                {
                    var cacheKey = $"leaderboard:activity:{activityId}:type:{(int)leaderboardType}";
                    var leaderboardData = await context.PlayerLeaderboards
                        .AsNoTracking()
                        .Include(pl => pl.Player)
                        .Where(pl => pl.ActivityId == activityId && pl.LeaderboardType == leaderboardType)
                        .OrderBy(pl => pl.Rank)
                        .Take(250)
                        .ToListAsync(ct);
                    await _cache.StringSetAsync(cacheKey, System.Text.Json.JsonSerializer.Serialize(leaderboardData));
                }
            }
        }

        private async Task ComputeDurationLeaderboards(AppDbContext context, List<Activity> activities, CancellationToken ct)
        {
            foreach (var activity in activities)
            {
                await using var cmd = new NpgsqlCommand("CALL compute_leaderboard_duration(@activityId)", context.Database.GetDbConnection() as NpgsqlConnection);
                cmd.Parameters.AddWithValue("activityId", activity.Id);
                await cmd.ExecuteNonQueryAsync(ct);
            }
        }

        private async Task ComputeTotalCompletionsLeaderboards(AppDbContext context, List<Activity> activities, CancellationToken ct)
        {
            foreach (var activity in activities)
            {
                await using var cmd = new NpgsqlCommand("CALL compute_leaderboard_completions(@activityId)", context.Database.GetDbConnection() as NpgsqlConnection);
                cmd.Parameters.AddWithValue("activityId", activity.Id);
                await cmd.ExecuteNonQueryAsync(ct);
            }
        }

        private async Task ComputeBestScoreLeaderboards(AppDbContext context, List<Activity> activities, CancellationToken ct)
        {
            foreach (var activity in activities)
            {
                await using var cmd = new NpgsqlCommand("CALL compute_leaderboard_score(@activityId)", context.Database.GetDbConnection() as NpgsqlConnection);
                cmd.Parameters.AddWithValue("activityId", activity.Id);
                await cmd.ExecuteNonQueryAsync(ct);
            }
        }
    }
}
