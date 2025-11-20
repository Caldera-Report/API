using Crawler.Services;
using Domain.Data;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Quartz;

namespace Crawler.Jobs;

/// <summary>
/// Quartz job that runs the pipeline once per day if the queue is empty.
/// </summary>
[DisallowConcurrentExecution]
public class PipelineOrchestratorJob : IJob
{
    private readonly PipelineOrchestrator _orchestrator;
    private readonly IDbContextFactory<AppDbContext> _contextFactory;
    private readonly ILogger<PipelineOrchestratorJob> _logger;

    public PipelineOrchestratorJob(PipelineOrchestrator orchestrator, IDbContextFactory<AppDbContext> contextFactory, ILogger<PipelineOrchestratorJob> logger)
    {
        _orchestrator = orchestrator;
        _contextFactory = contextFactory;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        var ct = context.CancellationToken;

        await using var db = await _contextFactory.CreateDbContextAsync(ct);
        var hasQueuedPlayers = await db.PlayerCrawlQueue.AnyAsync(p => p.Status == PlayerQueueStatus.Queued, ct);

        if (hasQueuedPlayers)
        {
            _logger.LogInformation("Quartz: skipping pipeline run at {Timestamp} because queued players already exist.", DateTimeOffset.UtcNow);
            return;
        }

        _logger.LogInformation("Quartz: starting pipeline run at {Timestamp}", DateTimeOffset.UtcNow);
        await _orchestrator.RunAsync(ct);
    }
}
