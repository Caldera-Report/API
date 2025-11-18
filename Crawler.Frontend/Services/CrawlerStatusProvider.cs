using System.Globalization;
using Crawler.Frontend.Models;
using Domain.Data;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;

namespace Crawler.Frontend.Services;

public interface ICrawlerStatusProvider
{
    Task<CrawlerStatusSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default);
}

public sealed class CrawlerStatusProvider : ICrawlerStatusProvider
{
    private static readonly RedisKey LastStartedKey = new("last-update-started");
    private static readonly RedisKey LastFinishedKey = new("last-update-finished");

    private readonly IConnectionMultiplexer _redis;
    private readonly IDbContextFactory<AppDbContext> _dbContextFactory;

    public CrawlerStatusProvider(
        IConnectionMultiplexer redis,
        IDbContextFactory<AppDbContext> dbContextFactory)
    {
        _redis = redis;
        _dbContextFactory = dbContextFactory;
    }

    public async Task<CrawlerStatusSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default)
    {
        var (totalPlayers, queuedPlayers, processingPlayers, completedPlayers, errorPlayers, oldestQueuedAt, lastProcessedAt) =
            await GetQueueMetricsAsync(cancellationToken);

        var db = _redis.GetDatabase();
        var lastStarted = await GetLatestListDateAsync(db, LastStartedKey);
        var lastFinished = await GetLatestListDateAsync(db, LastFinishedKey);

        var status = DetermineOverallStatus(queuedPlayers, processingPlayers, completedPlayers, errorPlayers, totalPlayers);

        return new CrawlerStatusSnapshot(
            Status: status,
            TotalPlayers: totalPlayers,
            QueuedPlayers: queuedPlayers,
            ProcessingPlayers: processingPlayers,
            CompletedPlayers: completedPlayers,
            ErrorPlayers: errorPlayers,
            OldestQueuedAt: oldestQueuedAt,
            LastProcessedAt: lastProcessedAt,
            LastRunStartedAt: lastStarted,
            LastRunFinishedAt: lastFinished
        );
    }

    private async Task<(long total, long queued, long processing, long completed, long errors, DateTime? oldestQueuedAt, DateTime? lastProcessedAt)> GetQueueMetricsAsync(CancellationToken cancellationToken)
    {
        await using var context = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var groupedCounts = await context.PlayerCrawlQueue
            .AsNoTracking()
            .GroupBy(queueItem => queueItem.Status)
            .Select(group => new
            {
                Status = group.Key,
                Count = group.LongCount()
            })
            .ToListAsync(cancellationToken);

        var totals = groupedCounts.ToDictionary(g => g.Status, g => g.Count);

        var total = groupedCounts.Sum(g => g.Count);
        var queued = totals.GetValueOrDefault(PlayerQueueStatus.Queued, 0);
        var processing = totals.GetValueOrDefault(PlayerQueueStatus.Processing, 0);
        var completed = totals.GetValueOrDefault(PlayerQueueStatus.Completed, 0);
        var errors = totals.GetValueOrDefault(PlayerQueueStatus.Error, 0);

        var oldestQueuedAt = await context.PlayerCrawlQueue
            .AsNoTracking()
            .Where(queueItem => queueItem.Status == PlayerQueueStatus.Queued)
            .OrderBy(queueItem => queueItem.EnqueuedAt)
            .Select(queueItem => (DateTime?)queueItem.EnqueuedAt)
            .FirstOrDefaultAsync(cancellationToken);

        var lastProcessedAt = await context.PlayerCrawlQueue
            .AsNoTracking()
            .Where(queueItem => queueItem.ProcessedAt != null)
            .OrderByDescending(queueItem => queueItem.ProcessedAt)
            .Select(queueItem => queueItem.ProcessedAt)
            .FirstOrDefaultAsync(cancellationToken);

        return (total, queued, processing, completed, errors, oldestQueuedAt, lastProcessedAt);
    }

    private static string DetermineOverallStatus(long queued, long processing, long completed, long errors, long total)
    {
        if (errors > 0)
        {
            return "Error";
        }

        if (processing > 0)
        {
            return "Processing";
        }

        if (queued > 0)
        {
            return "Queued";
        }

        if (total > 0 && completed == total)
        {
            return "Completed";
        }

        return "Idle";
    }

    private static async Task<DateTime?> GetLatestListDateAsync(IDatabase database, RedisKey key)
    {
        var listLength = await database.ListLengthAsync(key);
        if (listLength == 0)
        {
            return null;
        }

        var value = await database.ListGetByIndexAsync(key, -1);
        if (!value.HasValue)
        {
            return null;
        }

        return DateTime.TryParse(value.ToString(), null, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var parsed)
            ? DateTime.SpecifyKind(parsed, DateTimeKind.Utc)
            : null;
    }
}
