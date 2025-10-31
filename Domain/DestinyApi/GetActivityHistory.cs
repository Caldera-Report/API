namespace Domain.DestinyApi
{
    public class DestinyActivityHistoryResults
    {
        public List<DestinyHistoricalStatsPeriodGroup> activities { get; set; }
    }

    public class DestinyHistoricalStatsPeriodGroup
    {
        public DateTime period { get; set; }
        public DestinyHistoricalStatsActivity activityDetails { get; set; }
        public Dictionary<string, DestinyHistoricalStatsValue> values { get; set; }
    }

    public class DestinyHistoricalStatsActivity
    {
        public uint referenceId { get; set; }
        public uint directorActivityHash { get; set; }
        public string instanceId { get; set; }
        public int mode { get; set; }
        public List<int> modes { get; set; }
        public bool isPrivate { get; set; }
        public int membershipType { get; set; }
    }
}
