using System.Text.Json.Serialization;

namespace API.Models.DestinyApi.Search
{
    public class SearchResponse
    {
        [JsonPropertyName("searchResults")]
        public required List<SearchResult> SearchResults { get; set; }
    }
}
