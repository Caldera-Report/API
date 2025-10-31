namespace Domain.DestinyApi
{
    public class DestinyAggregateActivityResults
    {
        public List<DestinyAggregateActivityStats> activities { get; set; }
    }

    public class DestinyAggregateActivityStats
    {
        public uint activityHash { get; set; }
        public Dictionary<string, DestinyHistoricalStatsValue> values { get; set; }
    }

    public class DestinyHistoricalStatsValue
    {
        public string statId { get; set; }
        public DestinyHistoricalStatsValuePair basic { get; set; }
        public DestinyHistoricalStatsValuePair pga { get; set; }
        public string activityId { get; set; }
    }
    public class DestinyHistoricalStatsValuePair
    {
        public double value { get; set; }
        public string displayValue { get; set; }
    }
}
