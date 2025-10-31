using Domain.Data;
using Domain.DB;
using Domain.DestinyApi;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
using System.Threading.Channels;

namespace Crawler.Services
{
    public class PgcrProcessor
    {
        private readonly IDbContextFactory<AppDbContext> _contextFactory;
        private readonly IDatabase _cache;
        private readonly Dictionary<long, long> _activityHashMap;
        private readonly ChannelReader<PostGameCarnageReportData> _input;
        private readonly ILogger<PgcrProcessor> _logger;

        private const int MaxConcurrentTasks = 25;
        private static readonly SemaphoreSlim _playerInsertSemaphore = new(1, 1);

        public PgcrProcessor(ChannelReader<PostGameCarnageReportData> input, IConnectionMultiplexer redis, IDbContextFactory<AppDbContext> contextFactory, ILogger<PgcrProcessor> logger)
        {
            _input = input;
            _cache = redis.GetDatabase();
            _contextFactory = contextFactory;
            _logger = logger;

            var entries = _cache.HashGetAll("activityHashMappings");
            _activityHashMap = entries.ToDictionary(
                x => long.TryParse(x.Name, out var nameHash) ? nameHash : 0,
                x => long.TryParse(x.Value, out var valueHash) ? valueHash : 0
            );
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

        public async Task ProcessPgcrAsync(PostGameCarnageReportData pgcr, CancellationToken ct)
        {
            try
            {
                await using var context = await _contextFactory.CreateDbContextAsync(ct);

                var reportId = long.Parse(pgcr.activityDetails.instanceId);
                var report = await context.ActivityReports.FirstOrDefaultAsync(ar => ar.Id == reportId, ct);
                if (report is not null)
                {
                    if (!report.NeedsFullCheck)
                    {
                        _logger.LogTrace("PGCR {ReportId} already up to date.", reportId);
                        return;
                    }
                    context.ActivityReports.Remove(report);
                    await context.SaveChangesAsync(ct);
                }


                var activityId = _activityHashMap.TryGetValue(pgcr.activityDetails.referenceId, out var mappedId)
                    ? mappedId
                    : 0;

                var activityReport = new ActivityReport
                {
                    Id = reportId,
                    Date = pgcr.period,
                    ActivityId = activityId,
                    NeedsFullCheck = false
                };

                var publicEntries = pgcr.entries
                    .Where(e => e.player.destinyUserInfo.isPublic)
                    .ToList();

                var playerIds = publicEntries
                    .Select(e => long.Parse(e.player.destinyUserInfo.membershipId))
                    .Distinct()
                    .ToList();

                var existingPlayerIds = playerIds.Count > 0
                    ? await context.Players
                        .Where(p => playerIds.Contains(p.Id))
                        .Select(p => p.Id)
                        .ToListAsync(ct)
                    : new List<long>();

                var missingPlayerIds = playerIds
                    .Except(existingPlayerIds)
                    .ToList();
                var newPlayers = new List<Player>();

                if (missingPlayerIds.Count > 0)
                {
                    await _playerInsertSemaphore.WaitAsync(ct);
                    try
                    {
                        var refreshedExistingPlayerIds = await context.Players
                            .Where(p => playerIds.Contains(p.Id))
                            .Select(p => p.Id)
                            .ToListAsync(ct);

                        var confirmedMissingPlayerIds = playerIds
                            .Except(refreshedExistingPlayerIds)
                            .ToList();

                        if (confirmedMissingPlayerIds.Count > 0)
                        {
                            newPlayers = confirmedMissingPlayerIds
                                .Select(pid =>
                                {
                                    var entry = publicEntries.First(e => long.Parse(e.player.destinyUserInfo.membershipId) == pid);
                                    return new Player
                                    {
                                        Id = pid,
                                        MembershipType = (int)entry.player.destinyUserInfo.membershipType,
                                        DisplayName = entry.player.destinyUserInfo.displayName,
                                        DisplayNameCode = entry.player.destinyUserInfo.bungieGlobalDisplayNameCode
                                    };
                                })
                                .ToList();

                            context.Players.AddRange(newPlayers);

                            await context.SaveChangesAsync(ct);
                            refreshedExistingPlayerIds.AddRange(newPlayers.Select(p => p.Id));
                        }

                        existingPlayerIds = refreshedExistingPlayerIds;
                    }
                    finally
                    {
                        _playerInsertSemaphore.Release();
                    }
                }

                context.ActivityReports.Add(activityReport);

                var activityReportPlayers = publicEntries.Select(entry =>
                {
                    var membershipId = long.Parse(entry.player.destinyUserInfo.membershipId);
                    return new ActivityReportPlayer
                    {
                        PlayerId = membershipId,
                        ActivityReportId = reportId,
                        Score = (int)entry.values.score.basic.value,
                        Completed = entry.values.completed.basic.value == 1 && entry.values.completionReason.basic.value != 2.0, //comppleted, but did not fail
                        Duration = TimeSpan.FromSeconds(entry.values.activityDurationSeconds.basic.value)
                    };
                });

                await context.ActivityReportPlayers.AddRangeAsync(activityReportPlayers, ct);
                await context.SaveChangesAsync(ct);

                if (newPlayers.Count > 0)
                {
                    await _cache.ListRightPushAsync(
                        "player-crawl-queue",
                        newPlayers.Select(np => (RedisValue)np.Id).ToArray());
                    _logger.LogInformation("Queued {NewPlayerCount} new players discovered in PGCR {ReportId}.", newPlayers.Count, reportId);
                }
                else
                {
                    _logger.LogDebug("Processed PGCR {ReportId} with {PlayerCount} existing players.", reportId, existingPlayerIds.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing PGCR {ReportId}.", pgcr.activityDetails.instanceId);
            }
        }
    }
}
