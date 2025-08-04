using System.Text.Json.Serialization;

namespace API.Models.DestinyApi.Activity
{
    internal class ActivityResponse
    {
        [JsonPropertyName("activities")]
        public required List<Activity> Activities { get; set; }
    }
}
