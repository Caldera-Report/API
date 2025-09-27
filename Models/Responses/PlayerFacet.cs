using Classes.DB;
using Facet;
using Facet.Mapping;

namespace API.Models.Responses
{
    public class PlayerMapConfig : IFacetMapConfiguration<Player, PlayerDto>
    {
        public static void Map(Player source, PlayerDto target)
        {
            target.FullDisplayName = $"{source.DisplayName}#{source.DisplayNameCode}";
        }
    }

    [Facet(typeof(Player), exclude: [
        nameof(Player.LastPlayed),
        nameof(Player.LastPlayedActivityId),
        nameof(Player.LastUpdateStarted),
        nameof(Player.LastUpdateCompleted),
        nameof(Player.UpdatePriority),
        nameof(Player.LastUpdateStatus),
        nameof(Player.ActivityReports),
        nameof(Player.LastActivityReport),
        nameof(Player.NeedsFullCheck)],
        Configuration = typeof(PlayerMapConfig))]
    public partial class PlayerDto
    {
        public string FullDisplayName { get; set; }
    };
}
