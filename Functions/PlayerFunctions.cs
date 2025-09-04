using API.Services.Abstract;
using Azure;
using Classes.DTO;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FromBodyAttribute = Microsoft.Azure.Functions.Worker.Http.FromBodyAttribute;

namespace API.Functions;

public class PlayerFunctions
{
    private readonly ILogger<PlayerFunctions> _logger;
    private readonly IDestiny2Service _destiny2Service;

    public PlayerFunctions(ILogger<PlayerFunctions> logger, IDestiny2Service destiny2Service)
    {
        _logger = logger;
        _destiny2Service = destiny2Service;
    }

    [Function("SearchForPlayer")]
    public async Task<IActionResult> SearchForPlayer([HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "players/search")] HttpRequest req, [FromBody] SearchRequest request)
    {
        var requestBody = await req.ReadFromJsonAsync<SearchRequest>();
        var playerName = request.playerName;
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
            return new StatusCodeResult(500);
        }
    }

    [Function("GetPlayerStats")]
    public async Task<IActionResult> GetPlayerStats([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "players/{membershipTypeId}/{membershipId}")] HttpRequest req, int membershipTypeId, string membershipId)
    {
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
            return new StatusCodeResult(500);
        }
    }

    [Function("GetAllPlayers")]
    public async Task<IActionResult> GetAllPlayers([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "players")] HttpRequest req)
    {
        _logger.LogInformation("Get all players request");

        try
        {
            var players = await _destiny2Service.GetAllPlayers();
            var responseJson = JsonSerializer.Serialize(players);
            var etag = "\"" + Convert.ToBase64String(MD5.HashData(Encoding.UTF8.GetBytes(responseJson))) + "\"";

            if (req.Headers.TryGetValue("If-None-Match", out var inm) && inm == etag)
            {
                return new StatusCodeResult(StatusCodes.Status304NotModified);
            }

            req.HttpContext.Response.Headers["Cache-Control"] = "public, max-age=3600";
            req.HttpContext.Response.Headers["ETag"] = etag;
            return new ContentResult
            {
                Content = responseJson,
                StatusCode = StatusCodes.Status200OK,
                ContentType = "application/json" 
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting all players");
            return new StatusCodeResult(500);
        }
    }
}