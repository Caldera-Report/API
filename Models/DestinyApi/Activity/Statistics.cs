using System.Text.Json.Serialization;

namespace API.Models.DestinyApi.Activity
{
    public class Statistics
    {
        [JsonPropertyName("statId")]
        public required string StatId { get; set; }
        [JsonPropertyName("basic")]
        public required BasicStats Basic { get; set; }
        [JsonPropertyName("activityId")]
        public string? ActivityId { get; set; } 
    }
}
