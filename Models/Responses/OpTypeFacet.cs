using Classes.DB;
using Facet;
using Facet.Mapping;

namespace API.Models.Responses
{
    public class OpTypeMapConfig : IFacetMapConfiguration<OpType, OpTypeDto>
    {
        public static void Map(OpType source, OpTypeDto target)
        {
            target.ActivityTypes = source.ActivityTypes?.Select(at => new ActivityTypeDto(at)).ToArray();
        }
    }

    [Facet(typeof(OpType), exclude: [nameof(OpType.ActivityTypes)], Configuration = typeof(OpTypeMapConfig))]
    public partial class OpTypeDto
    {
        public ActivityTypeDto[] ActivityTypes { get; set; }
    };
}
