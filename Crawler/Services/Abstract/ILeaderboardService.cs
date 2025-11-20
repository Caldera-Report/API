namespace Crawler.Services.Abstract
{
    public interface ILeaderboardService
    {
        public Task ComputeLeaderboards(CancellationToken ct);
    }
}
