using Domain.DestinyApi;

namespace Domain.DTO
{
    public class PgcrWorkItem(PostGameCarnageReportData pgcr, long playerId)
    {
        public PostGameCarnageReportData Pgcr { get; set; } = pgcr;
        public long PlayerId { get; set; } = playerId;
    }
}
