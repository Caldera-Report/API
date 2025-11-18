using API.Clients.Abstract;
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
    public class CharacterCrawler
    {
        private readonly IDatabase _cache;
        private readonly IDbContextFactory<AppDbContext> _contextFactory;
        private readonly IDestiny2ApiClient _client;
        private readonly ChannelReader<CharacterWorkItem> _input;
        private readonly ChannelWriter<ActivityReportWorkItem> _output;
        private readonly ILogger<CharacterCrawler> _logger;
        private readonly ConcurrentDictionary<long, int> _playerActivityCount;
        private readonly ConcurrentDictionary<long, int> _playerCharacterWorkCount;

        private const int MaxConcurrentTasks = 20;
        private static readonly DateTime ActivityCutoffUtc = new DateTime(2025, 7, 15);
        private readonly Dictionary<long, long> _activityHashMap;

        public CharacterCrawler(
            IConnectionMultiplexer redis,
            IDestiny2ApiClient client,
            ChannelReader<CharacterWorkItem> input,
            ChannelWriter<ActivityReportWorkItem> output,
            ILogger<CharacterCrawler> logger,
            IDbContextFactory<AppDbContext> contextFactory,
            ConcurrentDictionary<long, int> playerActivityCount,
            ConcurrentDictionary<long, int> playerCharacterWorkCount,
            Dictionary<long, long> activityHashMap)
        {
            _cache = redis.GetDatabase();
            _client = client;
            _input = input;
            _output = output;
            _logger = logger;
            _contextFactory = contextFactory;
            _playerActivityCount = playerActivityCount;
            _playerCharacterWorkCount = playerCharacterWorkCount;
            _activityHashMap = activityHashMap;
        }

        public async Task RunAsync(CancellationToken ct)
        {
            var activeTasks = new List<Task>();
            _logger.LogInformation("Character crawler started.");
            try
            {
                await foreach (var item in _input.ReadAllAsync(ct))
                {
                    while (activeTasks.Count >= MaxConcurrentTasks)
                    {
                        var completedTask = await Task.WhenAny(activeTasks);
                        activeTasks.Remove(completedTask);
                    }

                    activeTasks.Add(ProcessItemAsync(item, ct));
                }

                await Task.WhenAll(activeTasks);
                _logger.LogInformation("Character crawler completed.");
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Character crawler cancellation requested.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled error in character crawler loop.");
            }
            finally
            {
                _output.Complete();
            }
        }

        private async Task ProcessItemAsync(CharacterWorkItem item, CancellationToken ct)
        {
            using var activity = CrawlerTelemetry.StartActivity("CharacterCrawler.ProcessItem");
            activity?.SetTag("crawler.player.id", item.PlayerId);
            activity?.SetTag("crawler.character.id", item.CharacterId);
            try
            {
                await using var context = await _contextFactory.CreateDbContextAsync(ct);
                var player = await context.Players.FindAsync(item.PlayerId, ct) ?? throw new InvalidDataException($"Player with Id {item.PlayerId} cannot be found");

                var reportIds = await GetCharacterActivityReports(player, item.LastPlayed, item.CharacterId, ct);
                var reportCount = reportIds.Length;
                if (reportCount > 0)
                {
                    _playerActivityCount.AddOrUpdate(player.Id, reportCount, (_, existing) => existing + reportCount);
                }

                foreach (var reportId in reportIds)
                {
                    await _output.WriteAsync(new ActivityReportWorkItem(reportId, player.Id), ct);
                }
                player.NeedsFullCheck = false;
                await context.SaveChangesAsync(ct);
                _logger.LogInformation(
                    "Queued {ReportCount} activity reports for player {PlayerId} character {CharacterId}.",
                    reportCount,
                    item.PlayerId,
                    item.CharacterId);
                activity?.SetTag("crawler.activity.reportCount", reportCount);
            }
            catch (Exception ex)
            {
                activity?.AddException(ex);
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                _logger.LogError(ex, "Error processing CharacterWorkItem for player: {PlayerId}, character: {CharacterId}", item.PlayerId, item.CharacterId);
                await using var context = await _contextFactory.CreateDbContextAsync(ct);
                var playerQueueItem = await context.PlayerCrawlQueue.FirstOrDefaultAsync(p => p.PlayerId == item.PlayerId, ct) 
                    ?? throw new InvalidOperationException($"Player with Id {item.PlayerId} cannot be found");
                var player = await context.Players.FirstOrDefaultAsync(p => p.Id == item.PlayerId, ct) 
                    ?? throw new InvalidOperationException($"Player with Id {item.PlayerId} cannot be found");

                playerQueueItem.Status = PlayerQueueStatus.Error;
                player.NeedsFullCheck = true;

                _playerActivityCount.TryRemove(item.PlayerId, out _);

                await context.SaveChangesAsync(ct);
            }
            finally
            {
                await FinalizeCharacterWorkAsync(item.PlayerId, ct);
            }
        }

        public async Task<long[]> GetCharacterActivityReports(Player player, DateTime lastPlayedActivityDate, string characterId, CancellationToken ct)
        {
            using var activity = CrawlerTelemetry.StartActivity("CharacterCrawler.GetActivityReports");
            activity?.SetTag("crawler.player.id", player.Id);
            activity?.SetTag("crawler.character.id", characterId);
            lastPlayedActivityDate = player.NeedsFullCheck ? ActivityCutoffUtc : lastPlayedActivityDate;

            var page = 0;
            var reportIds = new List<long>();
            var hasReachedLastUpdate = false;
            var activityCount = 250;

            try
            {
                while (!hasReachedLastUpdate)
                {
                    var response = await _client.GetHistoricalStatsForCharacter(player.Id, player.MembershipType, characterId, page, activityCount, ct);
                    if (response.Response?.activities == null || !response.Response.activities.Any())
                        break;
                    page++;
                    foreach (var activityReport in response.Response.activities)
                    {
                        hasReachedLastUpdate = activityReport.period <= lastPlayedActivityDate;
                        if (activityReport.period < ActivityCutoffUtc || hasReachedLastUpdate)
                            break;
                        var rawHash = activityReport.activityDetails.referenceId;
                        if (!_activityHashMap.TryGetValue(rawHash, out var canonicalId))
                            continue;
                        if (!long.TryParse(activityReport.activityDetails.instanceId, out var instanceId))
                            continue;

                        reportIds.Add(instanceId);
                    }
                    if (response.Response.activities.Last().period < ActivityCutoffUtc)
                        break;
                }
                return reportIds.ToArray();
            }
            catch (DestinyApiException ex) when (ex.ErrorCode == 1665)
            {
                activity?.SetStatus(ActivityStatusCode.Error, "Profile throttled");
                _logger.LogWarning(ex, "Historical stats throttled for player {PlayerId} character {CharacterId}.", player.Id, characterId);
                return Array.Empty<long>();
            }
            catch (Exception ex)
            {
                activity?.AddException(ex);
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                _logger.LogError(ex, "Error fetching activity reports for player: {PlayerId}, character: {CharacterId}", player.Id, characterId);
                throw;
            }
        }

        private async Task FinalizeCharacterWorkAsync(long playerId, CancellationToken ct)
        {
            var remaining = DecrementCharacterWorkCount(playerId);
            if (remaining > 0)
            {
                return;
            }

            _playerCharacterWorkCount.TryRemove(playerId, out _);

            var hasPendingActivities = _playerActivityCount.TryGetValue(playerId, out var pendingActivities);
            if (hasPendingActivities && pendingActivities > 0)
            {
                return;
            }

            if (hasPendingActivities && pendingActivities == 0)
            {
                _playerActivityCount.TryRemove(new KeyValuePair<long, int>(playerId, 0));
            }

            await using var context = await _contextFactory.CreateDbContextAsync(ct);
            var playerQueueItem = await context.PlayerCrawlQueue.FirstOrDefaultAsync(pcq => pcq.PlayerId == playerId, ct);
            if (playerQueueItem == null || playerQueueItem.Status != PlayerQueueStatus.Processing)
            {
                return;
            }

            playerQueueItem.Status = PlayerQueueStatus.Completed;
            playerQueueItem.ProcessedAt = DateTime.UtcNow;
            await context.SaveChangesAsync(ct);
            _logger.LogInformation("Completed crawling for player {PlayerId}; no pending PGCR work detected.", playerId);
        }

        private int DecrementCharacterWorkCount(long playerId)
        {
            while (true)
            {
                if (!_playerCharacterWorkCount.TryGetValue(playerId, out var current))
                {
                    return 0;
                }

                var updated = current <= 0 ? 0 : current - 1;
                if (_playerCharacterWorkCount.TryUpdate(playerId, updated, current))
                {
                    return updated;
                }
            }
        }
    }
}
