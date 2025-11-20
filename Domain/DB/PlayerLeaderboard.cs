using Domain.Enums;

namespace Domain.DB
{
    public class PlayerLeaderboard
    {
        public long PlayerId { get; set; }
        public string FullDisplayName { get; set; } = string.Empty;
        public long ActivityId { get; set; }
        public LeaderboardTypes LeaderboardType { get; set; }
        public int Rank { get; set; }
        public LeaderboardStat Data { get; set; } = new();
        public Player Player { get; set; }
    }

    public class LeaderboardStat
    {
        public int? Completions { get; set; }
        public int? Score { get; set; }
        public TimeSpan? Duration { get; set; }
    }
}
