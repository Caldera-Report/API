using API.Clients.Abstract;
using Domain.Data;
using Domain.DB;
using Domain.DestinyApi;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
using System.Threading.Channels;

namespace Crawler.Services
{
    public class ActivityReportCrawler
    {
        private IDbContextFactory<AppDbContext> _contextFactory;
        private readonly IDestiny2ApiClient _client;
        private readonly ChannelReader<long> _input;
        private readonly ChannelWriter<PostGameCarnageReportData> _output;
        private readonly ILogger<ActivityReportCrawler> _logger;

        private const int MaxConcurrentTasks = 200;

        public ActivityReportCrawler(ILogger<ActivityReportCrawler> logger, IDbContextFactory<AppDbContext> contextFactory, IDestiny2ApiClient client, ChannelReader<long> input, ChannelWriter<PostGameCarnageReportData> output)
        {
            _logger = logger;
            _contextFactory = contextFactory;
            _client = client;
            _output = output;
            _input = input;
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

        private async Task CrawlPgcrAsync(long activityReportId, CancellationToken ct)
        {
            try
            {
                await using var context = await _contextFactory.CreateDbContextAsync(ct);
                var activityReport = await context.ActivityReports.FirstOrDefaultAsync(ar => ar.Id == activityReportId, ct);
                if (activityReport == null || activityReport.NeedsFullCheck)
                {
                    var pgcr = (await _client.GetPostGameCarnageReport(activityReportId, ct)).Response;
                    await _output.WriteAsync(pgcr, ct);
                    _logger.LogDebug("Queued PGCR {ActivityReportId} for downstream processing.", activityReportId);
                }
                else
                {
                    _logger.LogTrace("Skipping PGCR {ActivityReportId}; existing report is current.", activityReportId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while crawling activity report: {id}", activityReportId);
            }
        }
    }
}
