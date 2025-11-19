namespace Crawler.Frontend.Services;

public interface ICrawlerTriggerService
{
    Task TriggerAsync(CancellationToken cancellationToken = default);
}
