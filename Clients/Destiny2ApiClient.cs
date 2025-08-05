using API.Clients.Abstract;
using API.Models.DestinyApi;
using API.Models.DestinyApi.Activity;
using API.Models.DestinyApi.Character;
using API.Models.DestinyApi.Search;
using API.Models.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace API.Clients
{
    public class Destiny2ApiClient : IDestiny2ApiClient
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;
        public Destiny2ApiClient(HttpClient httpClient, IOptions<Destiny2Options> options)
        {
            _httpClient = httpClient;
            _apiKey = options.Value.Token;

            if (string.IsNullOrEmpty(_apiKey))
            {
                throw new InvalidOperationException("Destiny2ApiToken configuration value is missing or empty");
            }

            _httpClient.BaseAddress = new Uri("https://www.bungie.net/Platform/");
            _httpClient.DefaultRequestHeaders.Add("X-API-Key", _apiKey);
        }
        public async Task<DestinyApiResponse<SearchResponse>> PerformSearch(string playerName)
        {
            var url = $"User/Search/Prefix/{Uri.EscapeDataString(playerName)}/0";
            var response = await _httpClient.GetAsync(url);

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<DestinyApiResponse<SearchResponse>>(content)
                    ?? throw new InvalidOperationException("Failed to deserialize response.");
            }
            else
            {
                throw new HttpRequestException($"Error fetching data: {response.ReasonPhrase}");
            }
        }

        public async Task<DestinyApiResponse<CharacterResponse>> GetCharactersForPlayer(string membershipId, int membershipType)
        {
            var url = $"Destiny2/{membershipType}/Profile/{membershipId}?components=Characters";
            var response = await _httpClient.GetAsync(url);

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<DestinyApiResponse<CharacterResponse>>(content)
                    ?? throw new InvalidOperationException("Failed to deserialize response.");
            }
            else
            {
                throw new HttpRequestException($"Error fetching data: {response.ReasonPhrase}");
            }
        }

        public async Task<DestinyApiResponse<ActivityResponse>> GetActivityAggregateForCharacter(string membershipId, int membershipType, string characterId)
        {
            var url = $"Destiny2/{membershipType}/Account/{membershipId}/Character/{characterId}/Stats/AggregateActivityStats";
            var response = await _httpClient.GetAsync(url);

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<DestinyApiResponse<ActivityResponse>>(content)
                    ?? throw new InvalidOperationException("Failed to deserialize response.");
            }
            else
            {
                throw new HttpRequestException($"Error fetching data: {response.ReasonPhrase}");
            }
        }
    }
}
