using System.Collections.Concurrent;
using System.Threading.RateLimiting;

namespace Crawler.Registries
{
    public class RateLimiterRegistry : IAsyncDisposable
    {
        private readonly ConcurrentDictionary<string, FixedWindowRateLimiter> _rateLimiters = new();
        private readonly int _permitLimit;

        public RateLimiterRegistry(int permitLimit = 20)
        {
            _permitLimit = permitLimit;
        }

        public FixedWindowRateLimiter GetRateLimiter(string key)
        {
            return _rateLimiters.GetOrAdd(key, _ => new FixedWindowRateLimiter(new FixedWindowRateLimiterOptions
            {
                PermitLimit = _permitLimit,
                Window = TimeSpan.FromSeconds(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            }));
        }

        public async Task<RateLimitLease> AcquireAsync(string key, CancellationToken cancellationToken = default)
        {
            var rateLimiter = GetRateLimiter(key);
            while (true)
            {
                var lease = await rateLimiter.AcquireAsync(1, cancellationToken);
                if (lease.IsAcquired)
                {
                    return lease;
                }
                await Task.Delay(50, cancellationToken);
            }
        }

        public async ValueTask DisposeAsync()
        {
            foreach (var rateLimiter in _rateLimiters.Values)
            {
                await rateLimiter.DisposeAsync();
            }
        }
    }
}
