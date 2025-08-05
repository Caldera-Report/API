using System.Text.Json.Serialization;

namespace API.Models.DestinyApi.Character
{
    public class CharacterData
    {
        [JsonPropertyName("data")]
        public required Dictionary<string, Character> Data { get; set; }
        [JsonPropertyName("privacy")]
        public required int Privacy { get; set; }
    }
}
