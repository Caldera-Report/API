using Domain.DB;
using Facet;

namespace API.Models.Responses
{

    [Facet(typeof(Player), exclude: [
        nameof(Player.ActivityReportPlayers),
        nameof(Player.NeedsFullCheck),
        nameof(Player.DisplayName),
        nameof(Player.DisplayNameCode),
        nameof(Player.LastPlayedCharacterBackgroundPath)])]
    public partial class PlayerSearchDto;
}
