using Domain.DB;
using Facet;
using Facet.Mapping;

namespace API.Models.Responses
{
    public class PlayerMapConfig : IFacetMapConfiguration<Player, PlayerDto>
    {
        public static void Map(Player source, PlayerDto target)
        {
            target.FullDisplayName = source.DisplayNameCode > 1000 ? $"{source.DisplayName}#{source.DisplayNameCode}" : $"{source.DisplayName}#0{source.DisplayNameCode}";
        }
    }

    [Facet(typeof(Player), exclude: [
        nameof(Player.LastUpdateStarted),
        nameof(Player.LastUpdateCompleted),
        nameof(Player.LastUpdateStatus),
        nameof(Player.NeedsFullCheck)],
        Configuration = typeof(PlayerMapConfig))]
    public partial class PlayerDto
    {
        public string FullDisplayName { get; set; }
    };
}
