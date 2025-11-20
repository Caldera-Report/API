using Domain.DB;
using Facet;
using Facet.Mapping;

namespace API.Domain.DTO.Responses
{
    public class ActivityReportPlayerMapConfig : IFacetMapConfiguration<ActivityReportPlayer, ActivityReportPlayerFacet>
    {
        public static void Map(ActivityReportPlayer source, ActivityReportPlayerFacet target)
        {
            target.InstanceId = source.ActivityReportId;
            target.Date = source.ActivityReport.Date;
        }
    }

    [Facet(typeof(ActivityReportPlayer),
        exclude: [nameof(ActivityReportPlayer.Player),
        nameof(ActivityReportPlayer.ActivityReport),
        nameof(ActivityReportPlayer.ActivityReportId)],
        Configuration = typeof(ActivityReportPlayerMapConfig)
    )]
    public partial class ActivityReportPlayerFacet
    {
        public long InstanceId { get; set; }
        public DateTime Date { get; set; }
    }
}
