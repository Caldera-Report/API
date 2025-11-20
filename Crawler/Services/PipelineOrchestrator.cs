using Crawler.Services.Abstract;
using Crawler.Telemetry;
using Domain.Data;
using Domain.DB;
using Domain.DestinyApi;
using Domain.DTO;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading.Channels;

namespace Crawler.Services
{
    public class PipelineOrchestrator
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IDbContextFactory<AppDbContext> _contextFactory;
        private readonly ILogger<PipelineOrchestrator> _logger;
        private readonly IHostEnvironment _env;

        public PipelineOrchestrator(ILogger<PipelineOrchestrator> logger, IServiceProvider serviceProvider, IHostEnvironment env, IDbContextFactory<AppDbContext> contextFactory)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
            _env = env;
            _contextFactory = contextFactory;
        }

        /// <summary>
        /// Executes the crawl pipeline end-to-end. Intended to be triggered by a scheduler.
        /// </summary>
        public async Task RunAsync(CancellationToken ct)
        {
            using var activity = CrawlerTelemetry.StartActivity("PipelineOrchestrator.Execute");
            activity?.SetTag("crawler.environment", _env.EnvironmentName);

            _logger.LogInformation("Pipeline orchestration started.");
            try
            {
                using var scope = _serviceProvider.CreateAsyncScope();
                var services = scope.ServiceProvider;

                using var db = services.GetRequiredService<IConnectionMultiplexer>();
                var cache = db.GetDatabase();
                await cache.ListRightPushAsync("last-update-started", DateTime.UtcNow.ToString("O"));

                await LoadPlayersIntoQueue(ct);
                var activityHashMap = await GetActivityHashMappings(cache);

                var characterChannel = Channel.CreateBounded<CharacterWorkItem>(new BoundedChannelOptions(10) { FullMode = BoundedChannelFullMode.Wait });
                var activityChannel = Channel.CreateBounded<ActivityReportWorkItem>(new BoundedChannelOptions(30) { FullMode = BoundedChannelFullMode.Wait });
                var pgcrProcessingChannel = Channel.CreateBounded<PgcrWorkItem>(new BoundedChannelOptions(100) { FullMode = BoundedChannelFullMode.Wait });

                var playerActivityCount = new ConcurrentDictionary<long, int>();
                var playerCharacterWorkCount = new ConcurrentDictionary<long, int>();

                var playerCrawler = ActivatorUtilities.CreateInstance<PlayerCrawler>(services, characterChannel.Writer, playerCharacterWorkCount);
                var characterCrawler = ActivatorUtilities.CreateInstance<CharacterCrawler>(services, characterChannel.Reader, activityChannel.Writer, playerActivityCount, playerCharacterWorkCount, activityHashMap);
                var activityCrawler = ActivatorUtilities.CreateInstance<ActivityReportCrawler>(services, activityChannel.Reader, pgcrProcessingChannel.Writer, playerActivityCount);
                var pgcrProcessor = ActivatorUtilities.CreateInstance<PgcrProcessor>(services, pgcrProcessingChannel.Reader, playerActivityCount, activityHashMap);

                var tasks = new List<Task>
                {
                    playerCrawler.RunAsync(ct),
                    characterCrawler.RunAsync(ct),
                    activityCrawler.RunAsync(ct),
                    pgcrProcessor.RunAsync(ct)
                };

                await Task.WhenAll(tasks);
                await cache.ListRightPushAsync("last-update-finished", DateTime.UtcNow.ToString("O"));

                var leaderboardService = services.GetRequiredService<ILeaderboardService>();

                await leaderboardService.ComputeLeaderboards(ct);

                _logger.LogInformation("Pipeline orchestration completed successfully.");
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Pipeline orchestration cancelled.");
            }
            catch (Exception ex)
            {
                activity?.AddException(ex);
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                _logger.LogError(ex, "Pipeline orchestration failed.");
                throw;
            }
        }

        private async Task LoadPlayersIntoQueue(CancellationToken ct)
        {
            using var activity = CrawlerTelemetry.StartActivity("PipelineOrchestrator.LoadPlayers");
            await using var context = await _contextFactory.CreateDbContextAsync(ct);

            try
            {
                await context.Database.ExecuteSqlRawAsync(
                """
                TRUNCATE TABLE "PlayerCrawlQueue";
                INSERT INTO "PlayerCrawlQueue"
                    ("Id","PlayerId","EnqueuedAt","ProcessedAt","Status","Attempts")
                SELECT
                    gen_random_uuid(),            -- or uuid_generate_v4() if that's what your DB uses
                    p."Id",
                    NOW(),
                    NULL,
                    {0},                          -- PlayerQueueStatus.Queued
                    0
                FROM "Players" p;
                """,
                (int)PlayerQueueStatus.Queued);
            }
            catch (Exception ex)
            {
                activity?.AddException(ex);
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                throw;
            }
        }

        private async Task<Dictionary<long, long>> GetActivityHashMappings(IDatabase cache)
        {
            var entries = await cache.HashGetAllAsync("activityHashMappings");
            return entries.ToDictionary(
                x => long.TryParse(x.Name, out var nameHash) ? nameHash : 0,
                x => long.TryParse(x.Value, out var valueHash) ? valueHash : 0
            );
        }
    }
}
