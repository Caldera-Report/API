using Classes.DB;
using Facet;
using Facet.Mapping;

namespace API.Models.Responses
{
    public class ActivityMapConfig : IFacetMapConfiguration<Activity, ActivityDto>
    {
        public static void Map(Activity source, ActivityDto target)
        {
            target.Name = source.Name.Contains(": Customize") ? source.Name[..^11] : source.Name;
        }
    }
    [Facet(typeof(Activity), exclude: [nameof(Activity.ActivityReports), nameof(Activity.ActivityType)], Configuration = typeof(ActivityMapConfig))]
    public partial class ActivityDto;
}
