using Classes.DB;
using Facet;
using Facet.Mapping;

namespace API.Models.Responses
{
    public class PlayerSearchMapConfig : IFacetMapConfiguration<Player, PlayerSearchDto>
    {
        public static void Map(Player source, PlayerSearchDto target)
        {
            target.FullDisplayName = source.DisplayNameCode > 1000 ? $"{source.DisplayName}#{source.DisplayNameCode}" : $"{source.DisplayName}#0{source.DisplayNameCode}";
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
        nameof(Player.NeedsFullCheck),
        nameof(Player.DisplayName),
        nameof(Player.DisplayNameCode),
        nameof(Player.LastPlayedCharacterBackgroundPath)],
        Configuration = typeof(PlayerSearchMapConfig))]
    public partial class PlayerSearchDto
    {
        public string FullDisplayName { get; set; }
    }
}
