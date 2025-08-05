using System.Text.Json.Serialization;

namespace API.Models.DestinyApi.Activity
{
    public class Activity
    {
        [JsonPropertyName("activityHash")]
        public required uint ActivityHash { get; set; }
        [JsonPropertyName("values")]
        public required Dictionary<string, Statistics> Values { get; set; }
    }
}
