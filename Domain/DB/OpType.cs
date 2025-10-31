namespace Domain.DB
{
    public class OpType
    {
        public int Id { get; set; }
        public string Name { get; set; }

        public ICollection<Activity> Activities { get; set; }
    }
}
