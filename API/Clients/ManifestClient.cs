using API.Clients.Abstract;
using Domain.Manifest;
using System.Text.Json;

namespace API.Clients
{
    public class ManifestClient : IManifestClient
    {
        private readonly HttpClient _client;
        public ManifestClient(HttpClient client)
        {
            _client = client;
            _client.BaseAddress = new Uri("https://www.bungie.net/");
        }

        public async Task<Dictionary<string, DestinyActivityDefinition>> GetActivityDefinitions(string url)
        {
            var response = await _client.GetAsync(url);
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<Dictionary<string, DestinyActivityDefinition>>(content)
                 ?? throw new InvalidOperationException("Failed to deserialize activity definitions.");
            }
            else
            {
                throw new HttpRequestException($"Error fetching activity definitions: {response.ReasonPhrase}");
            }
        }
    }
}
