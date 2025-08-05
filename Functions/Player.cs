using API.Services.Abstract;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace API.Functions;

public class Player
{
    private readonly ILogger<Player> _logger;
    private readonly TelemetryClient _telemetryClient;
    private readonly IDestiny2Service _destiny2Service;

    public Player(ILogger<Player> logger, TelemetryClient telemetryClient, IDestiny2Service destiny2Service)
    {
        _logger = logger;
        _telemetryClient = telemetryClient;
        _destiny2Service = destiny2Service;
    }

    [Function("Player")]
    public async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "player/{membershipTypeId}/{membershipId}")] HttpRequest req, int membershipTypeId, string membershipId)
    {
        using var operation = _telemetryClient.StartOperation<RequestTelemetry>("Player-Statistics");
        
        _logger.LogInformation($"Player statistics request for MembershipTypeId: {membershipTypeId}, MembershipId: {membershipId}");
        if (string.IsNullOrEmpty(membershipId) || membershipTypeId <= 0)
        {
            return new BadRequestObjectResult("Membership ID and type are required");
        }

        try
        {
            var results = await _destiny2Service.GetStatisticsForPlayer(membershipId, membershipTypeId);
            return new OkObjectResult(results);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error getting stats for player: {membershipId}");
            _telemetryClient.TrackException(ex);
            return new StatusCodeResult(500);
        }
    }
}