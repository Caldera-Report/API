namespace API.Models.Responses
{
    public class ActivityReportListDTO
    {
        public List<ActivityReportDto> Reports { get; set; } = new();
        public TimeSpan Average { get; set; }
        public ActivityReportDto? Best { get; set; }
    }
}
