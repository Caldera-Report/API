namespace Domain.DTO
{
    public class ActivityReportWorkItem(long activityReportId, long playerId)
    {
        public long ActivityReportId { get; set; } = activityReportId;
        public long PlayerId { get; set; } = playerId;
    }
}
