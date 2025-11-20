using Domain.Enums;

namespace Domain.DB
{
    public class PlayerCrawlQueue
    {
        public Guid Id { get; set; }
        public long PlayerId { get; set; }
        public DateTime EnqueuedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ProcessedAt { get; set; }
        public PlayerQueueStatus Status { get; set; } = PlayerQueueStatus.Queued;
        public int Attempts { get; set; } = 0;
    }
}
