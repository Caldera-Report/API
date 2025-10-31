using API.Clients.Abstract;
using Crawler.Registries;
using Domain.Configuration;
using Domain.DestinyApi;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace API.Clients
{
    public class Destiny2ApiClient : IDestiny2ApiClient
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;
        private const int MaxRetryAttempts = 3;
        private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(5);
        private readonly RateLimiterRegistry _rateLimiter;

        public Destiny2ApiClient(HttpClient httpClient, IOptions<Destiny2Options> options, RateLimiterRegistry rateLimiter)
        {
            _httpClient = httpClient;
            _apiKey = options.Value.Token!;
            _rateLimiter = rateLimiter;

            if (string.IsNullOrEmpty(_apiKey))
            {
                throw new InvalidOperationException("Destiny2ApiToken configuration value is missing or empty");
            }

            _httpClient.BaseAddress = new Uri("https://www.bungie.net/Platform/");
            _httpClient.DefaultRequestHeaders.Add("X-API-Key", _apiKey);
        }

        private async Task<HttpResponseMessage> SendWithRetryAsync(string endpoint, Func<Task<HttpResponseMessage>> sendAsync, CancellationToken ct)
        {
            for (var attempt = 1; attempt <= MaxRetryAttempts; attempt++)
            {
                try
                {
                    using var lease = await _rateLimiter.AcquireAsync(GetEndpointKey(endpoint), ct);

                    var response = await sendAsync();
                    if (response.IsSuccessStatusCode)
                    {
                        return response;
                    }

                    var exception = await CreateExceptionAsync(response);

                    if (exception is DestinyApiException destinyException &&
                        DestinyApiConstants.NonRetryableErrorCodes.Contains(destinyException.ErrorCode))
                    {
                        throw destinyException;
                    }

                    if (attempt == MaxRetryAttempts)
                    {
                        throw exception;
                    }
                }
                catch (DestinyApiException ex) when (!DestinyApiConstants.NonRetryableErrorCodes.Contains(ex.ErrorCode) && attempt < MaxRetryAttempts)
                {
                    // swallow to retry
                }
                catch (HttpRequestException) when (attempt < MaxRetryAttempts)
                {
                    // swallow to retry
                }
                catch (TaskCanceledException) when (attempt < MaxRetryAttempts)
                {
                    // swallow to retry
                }
                catch (Exception) when (attempt < MaxRetryAttempts)
                {
                    // swallow to retry
                }

                if (attempt < MaxRetryAttempts)
                {
                    await Task.Delay(RetryDelay);
                }
            }

            throw new InvalidOperationException("Unreachable code reached in SendWithRetryAsync.");
        }

        private static string GetEndpointKey(string path)
        {
            if (path.Contains("/Profile/", StringComparison.OrdinalIgnoreCase))
                return "characters";
            if (path.Contains("Stats/Activities", StringComparison.OrdinalIgnoreCase))
                return "activityReports";
            if (path.Contains("PostGameCarnageReport", StringComparison.OrdinalIgnoreCase))
                return "pgcr";
            return "default";
        }

        private static async Task<Exception> CreateExceptionAsync(HttpResponseMessage response)
        {
            var reason = response.ReasonPhrase;
            var statusCode = response.StatusCode;
            var content = await response.Content.ReadAsStringAsync();
            response.Dispose();

            var errorResponse = JsonSerializer.Deserialize<DestinyApiResponseError>(content);
            if (errorResponse != null)
            {
                return new DestinyApiException(errorResponse);
            }

            var fallbackMessage = reason ?? statusCode.ToString();
            var message = string.IsNullOrWhiteSpace(content)
                ? $"{(int)statusCode} {fallbackMessage}"
                : content;
            return new HttpRequestException($"Error fetching Data from the Bungie API: {message}");
        }

        public async Task<DestinyApiResponse<DestinyProfileResponse>> GetCharactersForPlayer(long membershipId, int membershipType, CancellationToken ct)
        {
            var url = $"Destiny2/{membershipType}/Profile/{membershipId}?components=100,200"; //Profile and Characters components
            try
            {
                using var response = await SendWithRetryAsync("GetCharactersForPlayer", () => _httpClient.GetAsync(url), ct);
                var content = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<DestinyApiResponse<DestinyProfileResponse>>(content)
                    ?? throw new InvalidOperationException("Failed to deserialize response.");
            }
            catch (DestinyApiException ex) when (ex.ErrorCode == 1665 && ex.Error is not null) // user is private
            {
                var error = ex.Error;
                return new DestinyApiResponse<DestinyProfileResponse>
                {
                    Response = new DestinyProfileResponse
                    {
                        characters = new DictionaryComponentResponseOfint64AndDestinyCharacterComponent()
                    },
                    ErrorStatus = error.ErrorStatus,
                    Message = error.Message,
                    MessageData = error.MessageData,
                    ErrorCode = error.ErrorCode,
                    ThrottleSeconds = error.ThrottleSeconds
                };
            }
        }

        public async Task<DestinyApiResponse<DestinyActivityHistoryResults>> GetHistoricalStatsForCharacter(long destinyMembershipId, int membershipType, string characterId, int page, int activityCount, CancellationToken ct)
        {
            var url = $"Destiny2/{membershipType}/Account/{destinyMembershipId}/Character/{characterId}/Stats/Activities/?page={page}&mode=7&count={activityCount}";
            using var response = await SendWithRetryAsync("GetHistoricalStatsForCharacter", () => _httpClient.GetAsync(url), ct);

            var content = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<DestinyApiResponse<DestinyActivityHistoryResults>>(content)
                ?? throw new InvalidOperationException("Failed to deserialize response.");
        }

        public async Task<DestinyApiResponse<PostGameCarnageReportData>> GetPostGameCarnageReport(long activityReportId, CancellationToken ct)
        {
            var url = $"https://stats.bungie.net/Platform/Destiny2/Stats/PostGameCarnageReport/{activityReportId}/";
            using var response = await SendWithRetryAsync("GetPostGameCarnageReport", () => _httpClient.GetAsync(url), ct);

            var content = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<DestinyApiResponse<PostGameCarnageReportData>>(content)
                ?? throw new InvalidOperationException("Failed to deserialize response.");
        }
    }
}
