using Domain.DestinyApi;
using Domain.DTO;
using StackExchange.Redis;
using System.Threading.Channels;

namespace Crawler.Services
{
    public class PipelineOrchestrator : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<PipelineOrchestrator> _logger;
        private readonly IHostEnvironment _env;
        private readonly IHostApplicationLifetime _appLifetime;

        public PipelineOrchestrator(ILogger<PipelineOrchestrator> logger, IServiceProvider serviceProvider, IHostEnvironment env, IHostApplicationLifetime applicationLifetime)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
            _env = env;
            _appLifetime = applicationLifetime;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Pipeline orchestration started.");
            try
            {
                using var scope = _serviceProvider.CreateAsyncScope();
                var services = scope.ServiceProvider;

                using var db = services.GetRequiredService<IConnectionMultiplexer>();
                var cache = db.GetDatabase();
                await cache.ListRightPushAsync("last-update-started", DateTime.UtcNow.ToString("O"));

                var characterChannel = Channel.CreateBounded<CharacterWorkItem>(new BoundedChannelOptions(10) { FullMode = BoundedChannelFullMode.Wait });
                var activityChannel = Channel.CreateBounded<long>(new BoundedChannelOptions(30) { FullMode = BoundedChannelFullMode.Wait });
                var pgcrProcessingChannel = Channel.CreateBounded<PostGameCarnageReportData>(new BoundedChannelOptions(100) { FullMode = BoundedChannelFullMode.Wait });

                var playerCrawler = ActivatorUtilities.CreateInstance<PlayerCrawler>(services, characterChannel.Writer);
                var characterCrawler = ActivatorUtilities.CreateInstance<CharacterCrawler>(services, characterChannel.Reader, activityChannel.Writer);
                var activityCrawler = ActivatorUtilities.CreateInstance<ActivityReportCrawler>(services, activityChannel.Reader, pgcrProcessingChannel.Writer);
                var pgcrProcessor = ActivatorUtilities.CreateInstance<PgcrProcessor>(services, pgcrProcessingChannel.Reader);

                var tasks = new List<Task>
                {
                    playerCrawler.RunAsync(stoppingToken),
                    characterCrawler.RunAsync(stoppingToken),
                    activityCrawler.RunAsync(stoppingToken),
                    pgcrProcessor.RunAsync(stoppingToken)
                };

                await Task.WhenAll(tasks);
                await cache.ListRightPushAsync("last-update-finished", DateTime.UtcNow.ToString("O"));

                var httpClientFactory = services.GetRequiredService<IHttpClientFactory>();
                var http = httpClientFactory.CreateClient(nameof(PipelineOrchestrator));
                if (_env.IsProduction())
                {
                    var securityKey = Environment.GetEnvironmentVariable("SecurityKey:ComputeLeaderboard");
                    if (!string.IsNullOrWhiteSpace(securityKey))
                    {
                        http.DefaultRequestHeaders.Add("x-security-key", securityKey);
                    }
                    else
                    {
                        _logger.LogWarning("Security key for leaderboard computation is not configured.");
                    }
                }

                var computeUri = _env.IsProduction()
                    ? new Uri("https://caldera-report-api.azurewebsites.net/api/activities/leaderboards/compute")
                    : new Uri("http://localhost:7187/api/activities/leaderboards/compute");

                var response = await http.PostAsync(computeUri, null, stoppingToken);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("Failed to trigger leaderboard computation. Status code: {StatusCode}", response.StatusCode);
                }
                else
                {
                    _logger.LogInformation("Triggered leaderboard computation successfully.");
                }

                _logger.LogInformation("Pipeline orchestration completed successfully.");
                _appLifetime.StopApplication();
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Pipeline orchestration cancelled.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Pipeline orchestration failed.");
                throw;
            }
        }
    }
}
