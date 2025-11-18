namespace Domain.DB
{
    public class Activity
    {
        public long Id { get; set; }
        public string Name { get; set; }
        public string ImageURL { get; set; }
        public int Index { get; set; }
        public int OpTypeId { get; set; }
        public bool Enabled { get; set; }

        public ICollection<ActivityReport> ActivityReports { get; set; }
        public OpType OpType { get; set; }
    }
}
