using System.Text.Json.Serialization;

namespace API.Models.DestinyApi.Search
{
    internal class DestinyMembership
    {
        [JsonPropertyName("isPublic")]
        public required bool IsPublic { get; set; }
        [JsonPropertyName("membershipType")]
        public required int MembershipType { get; set; }
        [JsonPropertyName("membershipId")]
        public required string MembershipId { get; set; }
    }
}
