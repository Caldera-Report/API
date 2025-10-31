using Domain.DB;
using Facet;

namespace API.Models.Responses
{
    [Facet(typeof(ActivityReport), exclude: nameof(ActivityReport.Activity))]
    public partial class ActivityReportDto;
}
