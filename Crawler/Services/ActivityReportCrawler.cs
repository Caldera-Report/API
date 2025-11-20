using API.Clients.Abstract;
using Crawler.Telemetry;
using Domain.Data;
using Domain.DB;
using Domain.DestinyApi;
using Domain.DTO;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
using System.Collections.Concurrent;
using System.Threading.Channels;
using System.Diagnostics;

namespace Crawler.Services
{
    public class ActivityReportCrawler
    {
        private IDbContextFactory<AppDbContext> _contextFactory;
        private readonly IDestiny2ApiClient _client;
        private readonly ChannelReader<ActivityReportWorkItem> _input;
        private readonly ChannelWriter<PgcrWorkItem> _output;
        private readonly ILogger<ActivityReportCrawler> _logger;
        private readonly ConcurrentDictionary<long, int> _playerActivityCount;

        private const int MaxConcurrentTasks = 200;

        public ActivityReportCrawler(
            ILogger<ActivityReportCrawler> logger,
            IDbContextFactory<AppDbContext> contextFactory,
            IDestiny2ApiClient client, ChannelReader<ActivityReportWorkItem> input,
            ChannelWriter<PgcrWorkItem> output,
            ConcurrentDictionary<long, int> playerActivityCount)
        {
            _logger = logger;
            _contextFactory = contextFactory;
            _client = client;
            _output = output;
            _input = input;
            _playerActivityCount = playerActivityCount;
        }

        public async Task RunAsync(CancellationToken ct)
        {
            var activeTasks = new List<Task>();
            _logger.LogInformation("Activity report crawler started.");
            try
            {
                await foreach (var item in _input.ReadAllAsync(ct))
                {
                    while (activeTasks.Count >= MaxConcurrentTasks)
                    {
                        var completedTask = await Task.WhenAny(activeTasks);
                        activeTasks.Remove(completedTask);
                    }

                    activeTasks.Add(CrawlPgcrAsync(item, ct));
                }

                await Task.WhenAll(activeTasks);
                _logger.LogInformation("Activity report crawler drained input channel.");
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Activity report crawler cancellation requested.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled error in activity report crawler loop.");
                throw;
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
                        _logger.LogDebug(ex, "Activity report crawler tasks ended during shutdown.");
                    }
                }

                _output.Complete();
            }
        }

        private async Task CrawlPgcrAsync(ActivityReportWorkItem item, CancellationToken ct)
        {
            using var activity = CrawlerTelemetry.StartActivity("ActivityReportCrawler.CrawlPgcr");
            activity?.SetTag("crawler.player.id", item.PlayerId);
            activity?.SetTag("crawler.activityReport.id", item.ActivityReportId);
            try
            {
                await using var context = await _contextFactory.CreateDbContextAsync(ct);
                var activityReport = await context.ActivityReports.FirstOrDefaultAsync(ar => ar.Id == item.ActivityReportId, ct);
                if (activityReport == null || activityReport.NeedsFullCheck)
                {
                    var pgcr = (await _client.GetPostGameCarnageReport(item.ActivityReportId, ct)).Response;
                    await _output.WriteAsync(new PgcrWorkItem(pgcr, item.PlayerId), ct);
                    _logger.LogDebug("Queued PGCR {ActivityReportId} for downstream processing.", item.ActivityReportId);
                }
                else
                {
                    var remaining = DecrementPlayerActivityCount(item.PlayerId);
                    if (remaining == 0)
                    {
                        var removed = TryRemovePlayerActivityCount(item.PlayerId);
                        if (!removed && HasPendingActivities(item.PlayerId))
                        {
                            _logger.LogTrace("Player {PlayerId} still has pending PGCR work despite zero decrement result.", item.PlayerId);
                        }
                        else
                        {
                            var playerQueueItem = await context.PlayerCrawlQueue.FirstOrDefaultAsync(pcq => pcq.PlayerId == item.PlayerId, ct)
                                ?? throw new InvalidOperationException($"Player queue item with playerId {item.PlayerId} does not exist");
                            if (playerQueueItem.Status != PlayerQueueStatus.Error)
                            {
                                playerQueueItem.Status = PlayerQueueStatus.Completed;
                                playerQueueItem.ProcessedAt = DateTime.UtcNow;
                                await context.SaveChangesAsync(ct);
                                _logger.LogInformation("Completed crawling for player {PlayerId}.", item.PlayerId);
                            }
                        }
                    }
                    _logger.LogTrace("Skipping PGCR {ActivityReportId}; existing report is current.", item.ActivityReportId);
                }
            }
            catch (Exception ex)
            {
                activity?.AddException(ex);
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                _logger.LogError(ex, "Error occurred while crawling activity report: {id}", item.ActivityReportId);
                await using var context = await _contextFactory.CreateDbContextAsync(ct);
                var playerQueueItem = await context.PlayerCrawlQueue.FirstOrDefaultAsync(pcq => pcq.PlayerId == item.PlayerId, ct)
                    ?? throw new InvalidOperationException($"Player with Id {item.PlayerId} cannot be found");
                var player = await context.Players.FirstOrDefaultAsync(p => p.Id == item.PlayerId, ct)
                    ?? throw new InvalidOperationException($"Player with Id {item.PlayerId} cannot be found");

                playerQueueItem.Status = PlayerQueueStatus.Error;
                var remaining = DecrementPlayerActivityCount(item.PlayerId);

                player.NeedsFullCheck = true;
                await context.SaveChangesAsync(ct);
                if (remaining == 0)
                {
                    TryRemovePlayerActivityCount(item.PlayerId);
                }
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
