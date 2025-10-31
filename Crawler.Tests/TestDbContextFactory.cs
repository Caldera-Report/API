using Domain.Data;
using Microsoft.EntityFrameworkCore;

namespace Crawler.Tests;

internal sealed class TestDbContextFactory : IDbContextFactory<AppDbContext>
{
    private readonly DbContextOptions<AppDbContext> _options;

    public TestDbContextFactory(DbContextOptions<AppDbContext> options)
    {
        _options = options;
    }

    public AppDbContext CreateDbContext()
    {
        return new AppDbContext(_options);
    }

    public ValueTask<AppDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
    {
        return new ValueTask<AppDbContext>(new AppDbContext(_options));
    }
}
