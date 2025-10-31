using API.Domain.DTO.Responses;
using API.Models.Responses;

namespace Domain.DTO.Responses
{
    public class ActivityReportListDTO
    {
        public List<ActivityReportPlayerFacet> Reports { get; set; } = new();
        public TimeSpan Average { get; set; }
        public ActivityReportPlayerFacet? Best { get; set; }
    }
}
