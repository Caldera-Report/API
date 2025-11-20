using Crawler.Telemetry;
using Domain.Data;
using Domain.DB;
using Domain.DestinyApi;
using Domain.DTO;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
using System.Diagnostics;
using System.Collections.Concurrent;
using System.Threading.Channels;

namespace Crawler.Services
{
    public class PgcrProcessor
    {
        private readonly IDbContextFactory<AppDbContext> _contextFactory;
        private readonly IDatabase _cache;
        private readonly Dictionary<long, long> _activityHashMap;
        private readonly ChannelReader<PgcrWorkItem> _input;
        private readonly ILogger<PgcrProcessor> _logger;
        private readonly ConcurrentDictionary<long, int> _playerActivityCount;

        private const int MaxConcurrentTasks = 25;
        private static readonly SemaphoreSlim _playerInsertSemaphore = new(1, 1);

        private static readonly ConcurrentDictionary<long, byte> _inFlightReports = new();

        public PgcrProcessor(ChannelReader<PgcrWorkItem> input,
            IConnectionMultiplexer redis,
            IDbContextFactory<AppDbContext> contextFactory,
            ILogger<PgcrProcessor> logger,
            ConcurrentDictionary<long, int> playerActivityCount,
            Dictionary<long, long> activityHashMap)
        {
            _input = input;
            _cache = redis.GetDatabase();
            _contextFactory = contextFactory;
            _logger = logger;
            _playerActivityCount = playerActivityCount;
            _activityHashMap = activityHashMap;
        }

        public async Task RunAsync(CancellationToken ct)
        {
            var activeTasks = new List<Task>();
            _logger.LogInformation("PGCR processor started.");

            try
            {
                await foreach (var pgcr in _input.ReadAllAsync(ct))
                {
                    activeTasks.Add(ProcessPgcrAsync(pgcr, ct));

                    if (activeTasks.Count >= MaxConcurrentTasks)
                    {
                        var finished = await Task.WhenAny(activeTasks);
                        activeTasks.Remove(finished);
                    }
                }

                await Task.WhenAll(activeTasks);
                _logger.LogInformation("PGCR processor completed.");
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("PGCR processor cancellation requested.");
            }
            finally
            {
                if (activeTasks.Count > 0)
                {
                    try
                    {
                        await Task.WhenAll(activeTasks);
                    }
                    catch (Exception ex) when (ex is OperationCanceledException or AggregateException)
                    {
                        _logger.LogDebug(ex, "PGCR processor tasks cancelled during shutdown.");
                    }
                }
            }
        }

        public async Task ProcessPgcrAsync(PgcrWorkItem item, CancellationToken ct)
        {
            using var activity = CrawlerTelemetry.StartActivity("PgcrProcessor.ProcessPgcr");
            activity?.SetTag("crawler.player.id", item.PlayerId);
            activity?.SetTag("crawler.pgcr.id", item.Pgcr.activityDetails.instanceId);

            var pgcr = item.Pgcr;
            var reportId = long.Parse(pgcr.activityDetails.instanceId);

            if (!_inFlightReports.TryAdd(reportId, 0))
            {
                _logger.LogDebug("Skipping PGCR {ReportId} - already processing", reportId);
                return;
            }

            try
            {
                await using var context = await _contextFactory.CreateDbContextAsync(ct);

                var existing = await context.ActivityReports.FirstOrDefaultAsync(ar => ar.Id == reportId, ct);
                if (existing != null && !existing.NeedsFullCheck)
                {
                    var remaining = DecrementPlayerActivityCount(item.PlayerId);
                    if (remaining == 0)
                    {
                        var removed = TryRemovePlayerActivityCount(item.PlayerId);
                        if (removed || !HasPendingActivities(item.PlayerId))
                        {
                            var playerQueueItem = await context.PlayerCrawlQueue.FirstOrDefaultAsync(pcq => pcq.PlayerId == item.PlayerId, ct);
                            if (playerQueueItem != null)
                            {
                                playerQueueItem.Status = PlayerQueueStatus.Completed;
                                playerQueueItem.ProcessedAt = DateTime.UtcNow;
                                await context.SaveChangesAsync(ct);
                            }
                        }
                    }
                    return;
                }
                if (existing != null && existing.NeedsFullCheck)
                {
                    context.ActivityReports.Remove(existing);
                    await context.SaveChangesAsync(ct);
                }

                var activityId = _activityHashMap.TryGetValue(pgcr.activityDetails.referenceId, out var mapped) ? mapped : 0;
                var activityReport = new ActivityReport
                {
                    Id = reportId,
                    Date = pgcr.period,
                    ActivityId = activityId,
                    NeedsFullCheck = false
                };
                context.ActivityReports.Add(activityReport);

                var currentCount = DecrementPlayerActivityCount(item.PlayerId);
                if (currentCount == 0)
                {
                    var removed = TryRemovePlayerActivityCount(item.PlayerId);
                    if (removed || !HasPendingActivities(item.PlayerId))
                    {
                        var playerQueueItem = await context.PlayerCrawlQueue.FirstOrDefaultAsync(pcq => pcq.PlayerId == item.PlayerId, ct)
                            ?? throw new InvalidOperationException($"Player Queue item with player ID {item.PlayerId} does not exist");
                        if (playerQueueItem.Status != PlayerQueueStatus.Error)
                        {
                            playerQueueItem.Status = Domain.Enums.PlayerQueueStatus.Completed;
                            playerQueueItem.ProcessedAt = DateTime.UtcNow;
                        }
                    }
                }

                await context.SaveChangesAsync(ct);

                var publicEntries = pgcr.entries.Where(e => e.player.destinyUserInfo.isPublic).ToList();

                if (publicEntries.Count > 0)
                {
                    await _playerInsertSemaphore.WaitAsync(ct);
                    try
                    {
                        var incomingIds = publicEntries
                            .Select(e => long.Parse(e.player.destinyUserInfo.membershipId))
                            .Distinct()
                            .ToList();

                        var existingIds = await context.Players
                            .Where(p => incomingIds.Contains(p.Id))
                            .Select(p => p.Id)
                            .ToListAsync(ct);

                        var missingIds = incomingIds.Except(existingIds).ToList();
                        if (missingIds.Count > 0)
                        {
                            var newPlayers = missingIds.Select(pid =>
                            {
                                var entry = publicEntries.First(e => long.Parse(e.player.destinyUserInfo.membershipId) == pid);
                                return new Player
                                {
                                    Id = pid,
                                    MembershipType = (int)entry.player.destinyUserInfo.membershipType,
                                    DisplayName = entry.player.destinyUserInfo.displayName,
                                    DisplayNameCode = entry.player.destinyUserInfo.bungieGlobalDisplayNameCode,
                                    FullDisplayName = entry.player.destinyUserInfo.displayName + "#" + entry.player.destinyUserInfo.bungieGlobalDisplayNameCode
                                };
                            }).ToList();

                            context.Players.AddRange(newPlayers);
                            foreach (var np in newPlayers)
                            {
                                context.PlayerCrawlQueue.Add(new PlayerCrawlQueue { PlayerId = np.Id });
                            }
                        }
                        await context.SaveChangesAsync(ct);
                    }
                    finally
                    {
                        _playerInsertSemaphore.Release();
                    }

                    var grouped = publicEntries.GroupBy(e => long.Parse(e.player.destinyUserInfo.membershipId)).ToDictionary(g => g.Key, g => g.ToList());

                    foreach (var group in grouped)
                    {
                        var membershipId = group.Key;
                        context.ActivityReportPlayers.Add(new ActivityReportPlayer
                        {
                            PlayerId = membershipId,
                            ActivityReportId = reportId,
                            Score = group.Value.Sum(e => (int)e.values.score.basic.value),
                            Completed = group.Value.All(e => e.values.completed.basic.value == 1 && e.values.completionReason.basic.value != 2.0),
                            Duration = TimeSpan.FromSeconds(group.Value.Sum(e => e.values.activityDurationSeconds.basic.value)),
                            ActivityId = activityId
                        });
                    }
                }

                await context.SaveChangesAsync(ct);
            }
            catch (Exception ex)
            {
                activity?.AddException(ex);
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                _logger.LogError(ex, "Error processing PGCR for player: {PlayerId}, PGCR Id: {PgcrId}", item.PlayerId, item.Pgcr.activityDetails.instanceId);
                await using var context = await _contextFactory.CreateDbContextAsync(ct);
                var remaining = DecrementPlayerActivityCount(item.PlayerId);

                var playerQueueItem = await context.PlayerCrawlQueue.FirstOrDefaultAsync(pcq => pcq.PlayerId == item.PlayerId, ct)
                    ?? throw new InvalidOperationException($"Player Queue item with player ID {item.PlayerId} does not exist");
                var player = await context.Players.FirstOrDefaultAsync(p => p.Id == item.PlayerId, ct)
                    ?? throw new InvalidOperationException($"Player with Id {item.PlayerId} cannot be found");
                playerQueueItem.Status = PlayerQueueStatus.Error;
                playerQueueItem.ProcessedAt = DateTime.UtcNow;
                player.NeedsFullCheck = true;
                await context.SaveChangesAsync(ct);
                if (remaining == 0)
                {
                    TryRemovePlayerActivityCount(item.PlayerId);
                }
            }
            finally
            {
                _inFlightReports.TryRemove(reportId, out _);
            }
        }

        private int DecrementPlayerActivityCount(long playerId)
        {
            while (true)
            {
                if (!_playerActivityCount.TryGetValue(playerId, out var current))
                {
                    return 0;
                }

                var updated = current <= 0 ? 0 : current - 1;
                if (_playerActivityCount.TryUpdate(playerId, updated, current))
                {
                    return updated;
                }
            }
        }

        private bool TryRemovePlayerActivityCount(long playerId)
        {
            return _playerActivityCount.TryRemove(new KeyValuePair<long, int>(playerId, 0));
        }

        private bool HasPendingActivities(long playerId)
        {
            return _playerActivityCount.TryGetValue(playerId, out var remaining) && remaining > 0;
        }
    }
}
