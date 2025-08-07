using API.Services.Abstract;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace API.Functions
{
    public class Search
    {
        private readonly ILogger<Search> _logger;
        private readonly TelemetryClient _telemetryClient;
        private readonly IDestiny2Service _destiny2Service;

        public Search(ILogger<Search> logger, TelemetryClient telemetryClient, IDestiny2Service destiny2Service)
        {
            _logger = logger;
            _telemetryClient = telemetryClient;
            _destiny2Service = destiny2Service;
        }

        [Function("Search")]
        public async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "search/{playerName}")] HttpRequest req,
            string playerName)
        {
            using var operation = _telemetryClient.StartOperation<RequestTelemetry>("Search-Function");

            _logger.LogInformation($"Search request for {playerName}");
            if (string.IsNullOrEmpty(playerName)) {
                return new BadRequestObjectResult("Player name is required");
            }

            try
            {
                var searchResults = await _destiny2Service.SearchForPlayer(playerName);
                return new OkObjectResult(searchResults);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error searching for player: {playerName}");
                _telemetryClient.TrackException(ex);
                operation.Telemetry.Success = false;
                return new StatusCodeResult(500);
            }
        }
    }
}
