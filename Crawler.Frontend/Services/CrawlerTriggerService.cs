using StackExchange.Redis;

namespace Crawler.Frontend.Services;

public class CrawlerTriggerService : ICrawlerTriggerService
{
    private const string ChannelName = "crawler:pipeline:run";
    private readonly IConnectionMultiplexer _redis;

    public CrawlerTriggerService(IConnectionMultiplexer redis)
    {
        _redis = redis;
    }

    public async Task TriggerAsync(CancellationToken cancellationToken = default)
    {
        var sub = _redis.GetSubscriber();
        await sub.PublishAsync(ChannelName, "manual-trigger");
    }
}
