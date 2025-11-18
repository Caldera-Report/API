using API.Helpers;
using API.Services.Abstract;
using Domain.DTO.Requests;
using Domain.Enums;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using FromBodyAttribute = Microsoft.Azure.Functions.Worker.Http.FromBodyAttribute;

namespace API.Functions;

public class ActivityFunctions
{
    private readonly IQueryService _queryService;
    private readonly IDestiny2Service _destiny2Service;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly ILogger<ActivityFunctions> _logger;

    public ActivityFunctions(IQueryService queryService, IDestiny2Service destiny2Service, ILogger<ActivityFunctions> logger, JsonSerializerOptions jsonSerializerOptions)
    {
        _queryService = queryService;
        _destiny2Service = destiny2Service;
        _logger = logger;
        _jsonOptions = jsonSerializerOptions;
    }

    [Function(nameof(GetActivities))]
    public async Task<IActionResult> GetActivities([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "activities")] HttpRequest req)
    {
        try
        {
            _logger.LogInformation("Processing activities list request.");
            var activities = await _queryService.GetAllActivitiesAsync();
            return ResponseHelpers.CachedJson(req, activities, _jsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve activities.");
            return new StatusCodeResult(500);
        }
    }

    [Function(nameof(CacheActivities))]
    public async Task CacheActivities([TimerTrigger("0 0 0 * * *")] TimerInfo timer)
    {
        try
        {
            _logger.LogInformation("Caching all activities (timer trigger).");
            await _queryService.CacheAllActivitiesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error caching activities during scheduled run.");
        }
    }

    [Function(nameof(GetLeaderboard))]
    public async Task<IActionResult> GetLeaderboard([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "activities/leaderboards/{leaderboardType}/{activityId}")] HttpRequest req, string leaderboardType, long activityId)
    {
        try
        {
            _logger.LogInformation("Retrieving completions leaderboard for {ActivityId}.", activityId);
            var queryParams = req.Query;
            var count = queryParams.ContainsKey("count") && int.TryParse(queryParams["count"], out var parsedCount) ? parsedCount : 250;
            var offset = queryParams.ContainsKey("offset") && int.TryParse(queryParams["offset"], out var parsedOffset) ? parsedOffset : 0;
            LeaderboardTypes type;
            switch (leaderboardType.ToLower())
            {
                case "completions":
                    type = LeaderboardTypes.TotalCompletions;
                    break;
                case "speed":
                    type = LeaderboardTypes.FastestCompletion;
                    break;
                default:
                    type = LeaderboardTypes.HighestScore;
                    break;
            }
            var leaderboard = await _queryService.GetLeaderboardAsync(activityId, type, count, offset);
            return ResponseHelpers.CachedJson(req, leaderboard, _jsonOptions, 300);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving completions leaderboard for {ActivityId}.", activityId);
            return new StatusCodeResult(500);
        }
    }

    [Function(nameof(SearchForPlayerLeaderboard))]
    public async Task<IActionResult> SearchForPlayerLeaderboard([HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "activities/leaderboards/{leaderboardType}/{activityId}/search")] HttpRequest req, string leaderboardType, long activityId, [FromBody] SearchRequest request)
    {
        try
        {
            _logger.LogInformation("Searching for player leaderboard entries for activity {ActivityId}.", activityId);
            var playerName = request.playerName;
            LeaderboardTypes type;
            switch (leaderboardType.ToLower())
            {
                case "completions":
                    type = LeaderboardTypes.TotalCompletions;
                    break;
                case "speed":
                    type = LeaderboardTypes.FastestCompletion;
                    break;
                default:
                    type = LeaderboardTypes.HighestScore;
                    break;
            }
            var leaderboardEntries = await _queryService.GetLeaderboardsForPlayer(playerName, activityId, type);
            return ResponseHelpers.CachedJson(req, leaderboardEntries, _jsonOptions, 300);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching for player leaderboard entries for activity {ActivityId}.", activityId);
            return new StatusCodeResult(500);
        }
    }

    [Function(nameof(BuildActivityMappings))]
    public async Task BuildActivityMappings([TimerTrigger("0 0 11 * * *")] TimerInfo timer)
    {
        try
        {
            await _destiny2Service.GroupActivityDuplicates();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error Grouping activities.");
        }
    }
}
