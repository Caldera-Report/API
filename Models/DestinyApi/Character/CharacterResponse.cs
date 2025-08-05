using System.Text.Json.Serialization;

namespace API.Models.DestinyApi.Character
{
    public class CharacterResponse
    {
        [JsonPropertyName("responseMintedTimestamp")]
        public DateTime ResponseMinted { get; set; }
        [JsonPropertyName("secondaryComponentsMintedTimestamp")]
        public DateTime SecondaryComponentsMinted { get; set; }
        [JsonPropertyName("characters")]
        public required CharacterData CharacterData { get; set; }
    }
}
