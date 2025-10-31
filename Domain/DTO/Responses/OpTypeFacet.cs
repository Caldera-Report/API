using Domain.DB;
using Facet;
using Facet.Mapping;

namespace API.Models.Responses
{
    public class OpTypeMapConfig : IFacetMapConfiguration<OpType, OpTypeDto>
    {
        public static void Map(OpType source, OpTypeDto target)
        {
            target.Activities = source.Activities?.Select(a => new ActivityDto(a)).ToArray();
        }
    }

    [Facet(typeof(OpType), exclude: [nameof(OpType.Activities)], Configuration = typeof(OpTypeMapConfig))]
    public partial class OpTypeDto
    {
        public ActivityDto[] Activities { get; set; }
    };
}
