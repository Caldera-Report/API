using API.Clients.Abstract;
using API.Configuration;
using Classes.DestinyApi;
using Microsoft.Extensions.Options;
using System.Net.Http.Json;
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
            _apiKey = options.Value.Token!;

            if (string.IsNullOrEmpty(_apiKey))
            {
                throw new InvalidOperationException("Destiny2ApiToken configuration value is missing or empty");
            }

            _httpClient.BaseAddress = new Uri("https://www.bungie.net/Platform/");
            _httpClient.DefaultRequestHeaders.Add("X-API-Key", _apiKey);
        }
        public async Task<DestinyApiResponse<UserSearchPrefixResponse>> PerformSearchByPrefix(UserSearchPrefixRequest name, int page)
        {
            var url = $"User/Search/GlobalName/{page}";
            var response = await _httpClient.PostAsync(url, JsonContent.Create(name));

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<DestinyApiResponse<UserSearchPrefixResponse>>(content)
                    ?? throw new InvalidOperationException("Failed to deserialize response.");
            }
            else
            {
                throw new HttpRequestException($"Error fetching data: {response.ReasonPhrase}");
            }
        }

        public async Task<DestinyApiResponse<List<UserInfoCard>>> PerformSearchByBungieName(ExactSearchRequest player, int membershipTypeId)
        {
            var url = $"Destiny2/SearchDestinyPlayerByBungieName/{membershipTypeId}/";
            var response = await _httpClient.PostAsync(url, JsonContent.Create(player));
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<DestinyApiResponse<List<UserInfoCard>>>(content)
                    ?? throw new InvalidOperationException("Failed to deserialize response.");
            }
            else
            {
                throw new HttpRequestException($"Error fetching data: {response.ReasonPhrase}");
            }
        }

        public async Task<DestinyApiResponse<DestinyProfileResponse>> GetCharactersForPlayer(long membershipId, int membershipType)
        {
            var url = $"Destiny2/{membershipType}/Profile/{membershipId}?components=Characters";
            var response = await _httpClient.GetAsync(url);

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<DestinyApiResponse<DestinyProfileResponse>>(content)
                    ?? throw new InvalidOperationException("Failed to deserialize response.");
            }
            else
            {
                throw new HttpRequestException($"Error fetching data: {response.ReasonPhrase}");
            }
        }

        public async Task<DestinyApiResponse<DestinyAggregateActivityResults>> GetActivityAggregateForCharacter(long membershipId, int membershipType, string characterId)
        {
            var url = $"Destiny2/{membershipType}/Account/{membershipId}/Character/{characterId}/Stats/AggregateActivityStats";
            var response = await _httpClient.GetAsync(url);

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<DestinyApiResponse<DestinyAggregateActivityResults>>(content)
                    ?? throw new InvalidOperationException("Failed to deserialize response.");
            }
            else
            {
                throw new HttpRequestException($"Error fetching data: {response.ReasonPhrase}");
            }
        }
        public async Task<DestinyApiResponse<DestinyActivityHistoryResults>> GetHistoricalStatsForCharacter(long destinyMembershipId, int membershipType, string characterId, int page, int activityCount)
        {
            var url = $"Destiny2/{membershipType}/Account/{destinyMembershipId}/Character/{characterId}/Stats/Activities/?page={page}&mode=7&count={activityCount}";
            var req = new HttpRequestMessage(HttpMethod.Get, url);

            using var response = await _httpClient.SendAsync(req);

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<DestinyApiResponse<DestinyActivityHistoryResults>>(content)
                    ?? throw new InvalidOperationException("Failed to deserialize response.");
            }
            else
            {
                var content = await response.Content.ReadAsStringAsync();
                var errorResponse = JsonSerializer.Deserialize<DestinyApiResponseError>(content);
                if (errorResponse.ErrorCode == 1665) //user is private
                    return new DestinyApiResponse<DestinyActivityHistoryResults>
                    {
                        Response = new DestinyActivityHistoryResults { activities = [] },
                        ErrorStatus = errorResponse.ErrorStatus,
                        Message = errorResponse.Message,
                        MessageData = errorResponse.MessageData,
                        ErrorCode = errorResponse.ErrorCode,
                        ThrottleSeconds = errorResponse.ThrottleSeconds
                    };
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    throw new HttpRequestException($"Error fetching data from Bungie API: {errorContent}");
                }
            }
        }
    }
}
