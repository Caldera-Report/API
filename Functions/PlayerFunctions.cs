using API.Helpers;
using API.Services.Abstract;
using Classes.DTO;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using FromBodyAttribute = Microsoft.Azure.Functions.Worker.Http.FromBodyAttribute;

namespace API.Functions;

public class PlayerFunctions
{
    private readonly IDestiny2Service _destiny2Service;
    private readonly IQueryService _queryService;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly ILogger<PlayerFunctions> _logger;

    public PlayerFunctions(ILogger<PlayerFunctions> logger, IDestiny2Service destiny2Service, IQueryService queryService, JsonSerializerOptions jsonSerializerOptions)
    {
        _logger = logger;
        _destiny2Service = destiny2Service;
        _queryService = queryService;
        _jsonOptions = jsonSerializerOptions;
    }

    [Function("SearchForPlayer")]
    public async Task<IActionResult> SearchForPlayer([HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "players/search")] HttpRequest req, [FromBody] SearchRequest request)
    {
        var playerName = request.playerName;
        _logger.LogInformation($"Search request for {playerName}");
        if (string.IsNullOrEmpty(playerName)) {
            return new BadRequestObjectResult("Player name is required");
        }
        try
        {
            var searchResults = await _destiny2Service.SearchForPlayer(playerName);
            JsonSerializer.Serialize(searchResults, _jsonOptions);
            return new ContentResult
            {
                Content = JsonSerializer.Serialize(searchResults, _jsonOptions),
                StatusCode = StatusCodes.Status200OK,
                ContentType = "application/json"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error searching for player: {playerName}");
            return new StatusCodeResult(500);
        }
    }

    [Function("GetPlayer")]
    public async Task<IActionResult> GetPlayer([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "players/{membershipId}")] HttpRequest req, long membershipId)
    {
        _logger.LogInformation($"Player request for MembershipId: {membershipId}");


        if (membershipId <= 0)
        {
            return new BadRequestObjectResult("Membership ID and type are required");
        }

        try
        {
            var results = await _queryService.GetPlayerAsync(membershipId);
            return ResponseHelpers.CachedJson(req, results, _jsonOptions, 0);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error getting player: {membershipId}");
            return new StatusCodeResult(500);
        }
    }

    [Function("GetAllPlayers")]
    public async Task<IActionResult> GetAllPlayers([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "players")] HttpRequest req)
    {
        _logger.LogInformation("Get all players request");

        try
        {
            var players = await _queryService.GetAllPlayersAsync();
            return ResponseHelpers.CachedJson(req, players, _jsonOptions, 0);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting all players");
            return new StatusCodeResult(500);
        }
    }

    [Function("GetPlayerStatsPerActivity")]
    public async Task<IActionResult> GetPlayerStatsForActivity([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "players/{membershipId}/stats/{activityId}")] HttpRequest req, long membershipId, long activityId)
    {
        _logger.LogInformation($"Get stats for activity request recieved for player {membershipId} for activity {activityId}");
        
        try
        {
            var reports = await _queryService.GetPlayerReportsForActivityAsync(membershipId, activityId);
            return ResponseHelpers.CachedJson(req, reports, _jsonOptions, 300);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error getting stats for player: {membershipId} for activity {activityId}");
            return new StatusCodeResult(500);
        }
    }

    [Function("LoadPlayerActivities")]
    public async Task<IActionResult> LoadPlayerActivities([HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "players/{membershipId}/load")] HttpRequest req, long membershipId)
    {
        _logger.LogInformation($"Load activities request for player {membershipId} ");
        if (membershipId <= 0)
        {
            return new BadRequestObjectResult("Membership ID and type are required");
        }
        try
        {
            var player = await _queryService.GetPlayerDbObject(membershipId);

            if (DateTime.UtcNow - player.LastUpdateCompleted < TimeSpan.FromMinutes(5))
                return new NoContentResult();

            var characters = await _destiny2Service.GetCharactersForPlayer(membershipId, player.MembershipType);
            foreach (var character in characters)
            {
                _logger.LogInformation($"Loading activities for character {character.Key} of player {membershipId}");
                await _destiny2Service.LoadPlayerActivityReports(player, character.Key);
            }

            var lastplayed = characters.Values.OrderByDescending(c => c.dateLastPlayed).FirstOrDefault();

            await _queryService.UpdatePlayerEmblems(player, lastplayed?.emblemBackgroundPath ?? "", lastplayed?.emblemPath ?? "");

            return new OkObjectResult(new { Success = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error loading activities for player: {membershipId}");
            return new StatusCodeResult(500);
        }
    }
}