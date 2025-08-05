using System.Text.Json.Serialization;

namespace API.Models.DestinyApi.Search
{
    public class SearchResult
    {
        [JsonPropertyName("bungieGlobalDisplayName")]
        public required string BungieGlobalDisplayName { get; set; }
        [JsonPropertyName("bungieGlobalDisplayNameCode")]
        public required int BungieGlobalDisplayNameCode { get; set; }
        [JsonPropertyName("destinyMemberships")]
        public required List<DestinyMembership> DestinyMemberships { get; set; }
    }
}
