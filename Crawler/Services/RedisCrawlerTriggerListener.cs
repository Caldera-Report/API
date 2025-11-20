using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Quartz;
using StackExchange.Redis;

namespace Crawler.Services;

/// <summary>
/// Subscribes to a Redis channel and triggers the pipeline job on demand.
/// </summary>
public class RedisCrawlerTriggerListener : BackgroundService
{
    private const string ChannelName = "crawler:pipeline:run";

    private readonly IConnectionMultiplexer _redis;
    private readonly ISchedulerFactory _schedulerFactory;
    private readonly ILogger<RedisCrawlerTriggerListener> _logger;

    public RedisCrawlerTriggerListener(IConnectionMultiplexer redis, ISchedulerFactory schedulerFactory, ILogger<RedisCrawlerTriggerListener> logger)
    {
        _redis = redis;
        _schedulerFactory = schedulerFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var subscriber = _redis.GetSubscriber();

        await subscriber.SubscribeAsync(ChannelName, async (_, __) =>
        {
            try
            {
                var scheduler = await _schedulerFactory.GetScheduler(stoppingToken);
                await scheduler.TriggerJob(new JobKey("PipelineOrchestratorJob"), stoppingToken);
                _logger.LogInformation("Received Redis trigger; job enqueued.");
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to trigger pipeline from Redis message.");
            }
        });

        _logger.LogInformation("Subscribed to Redis channel {Channel}", ChannelName);
    }
}
