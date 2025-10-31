namespace Domain.DB
{
    public class ActivityReportPlayer
    {
        public long PlayerId { get; set; }
        public long ActivityReportId { get; set; }
        public int Score { get; set; }
        public bool Completed { get; set; }
        public TimeSpan Duration { get; set; }

        public Player Player { get; set; }
        public ActivityReport ActivityReport { get; set; }
    }
}
