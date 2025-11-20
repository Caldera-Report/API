using Domain.DB;
using Facet;

namespace API.Models.Responses
{
    [Facet(typeof(Player), exclude: [
        nameof(Player.NeedsFullCheck)])]
    public partial class PlayerDto;
}
