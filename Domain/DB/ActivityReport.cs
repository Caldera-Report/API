namespace Domain.DB
{
    public class ActivityReport
    {
        public long Id { get; set; }
        public DateTime Date { get; set; }
        public long ActivityId { get; set; }
        public bool NeedsFullCheck { get; set; }

        public List<ActivityReportPlayer> Players { get; set; }
        public Activity Activity { get; set; }
    }
}