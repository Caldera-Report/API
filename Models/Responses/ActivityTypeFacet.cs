using Classes.DB;
using Facet;
using Facet.Mapping;

namespace API.Models.Responses
{
    public class ActivityTypeMapConfig : IFacetMapConfiguration<ActivityType, ActivityTypeDto>
    {
        public static void Map(ActivityType source, ActivityTypeDto target)
        {
            target.Activities = source.Activities?.Select(a => new ActivityDto(a)).ToArray();
        }
    }

    [Facet(typeof(ActivityType), exclude: [nameof(ActivityType.Activities), nameof(ActivityType.OpType)], Configuration = typeof(ActivityTypeMapConfig))]
    public partial class ActivityTypeDto
    {
        public ActivityDto[] Activities { get; set; }
    };
}
