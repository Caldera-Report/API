using System.Text.Json.Serialization;

namespace API.Models.DestinyApi.Activity
{
    public class ActivityResponse
    {
        [JsonPropertyName("activities")]
        public required List<Activity> Activities { get; set; }
    }
}
