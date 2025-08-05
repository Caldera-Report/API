using System.Text.Json.Serialization;

namespace API.Models.DestinyApi.Activity
{
    public class BasicStats
    {
        [JsonPropertyName("value")]
        public required double Value { get; set; }
        [JsonPropertyName("displayValue")]
        public required string DisplayValue { get; set; }
    }
}
