namespace API.Models.DB
{
    public class ActivityReport
    {
        public long ID { get; set; }
        public DateTime Date { get; set; }
        public long ActivityId { get; set; }
        public bool Completed { get; set; }
        public long Duration { get; set; }
    }
}
