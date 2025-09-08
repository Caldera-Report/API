using Classes.DB;
using Facet;

namespace API.Models.Responses
{
    [Facet(typeof(ActivityReport), exclude: [nameof(ActivityReport.Player), nameof(ActivityReport.Activity)])]
    public partial class ActivityReportDto;
}
