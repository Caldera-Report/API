using System.Text.Json.Serialization;

namespace API.Models.DestinyApi.Character
{
    public class Character
    {
        [JsonPropertyName("membershipId")]
        public required string MembershipId { get; set; }
        [JsonPropertyName("membershipType")]
        public required int MembershipType { get; set; }
        [JsonPropertyName("characterId")]
        public required string CharacterId { get; set; }
        [JsonPropertyName("light")]
        public required int Light { get; set; }
    }
}
