using API.Helpers;
using API.Services.Abstract;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using System.Linq;
using System.Text.Json;

namespace API.Functions;

public class ActivityFunctions
{
    private readonly IQueryService _queryService;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly ILogger<ActivityFunctions> _logger;

    public ActivityFunctions(IQueryService queryService, ILogger<ActivityFunctions> logger, JsonSerializerOptions jsonSerializerOptions)
    {
        _queryService = queryService;
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

    [Function(nameof(GetCompletionsLeaderboard))]
    public async Task<IActionResult> GetCompletionsLeaderboard([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "activities/leaderboards/completions/{activityId}")] HttpRequest req, long activityId)
    {
        try
        {
            _logger.LogInformation("Retrieving completions leaderboard for {ActivityId}.", activityId);
            var leaderboard = await _queryService.GetCompletionsLeaderboardAsync(activityId);
            return ResponseHelpers.CachedJson(req, leaderboard, _jsonOptions, 300);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving completions leaderboard for {ActivityId}.", activityId);
            return new StatusCodeResult(500);
        }
    }

    [Function(nameof(GetSpeedLeaderboard))]
    public async Task<IActionResult> GetSpeedLeaderboard([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "activities/leaderboards/speed/{activityId}")] HttpRequest req, long activityId)
    {
        try
        {
            _logger.LogInformation("Retrieving speed leaderboard for {ActivityId}.", activityId);
            var leaderboard = await _queryService.GetSpeedLeaderboardAsync(activityId);
            return ResponseHelpers.CachedJson(req, leaderboard, _jsonOptions, 300);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving speed leaderboard for {ActivityId}.", activityId);
            return new StatusCodeResult(500);
        }
    }

    [Function(nameof(GetTotalTimeLeaderboard))]
    public async Task<IActionResult> GetTotalTimeLeaderboard([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "activities/leaderboards/totalTime/{activityId}")] HttpRequest req, long activityId)
    {
        try
        {
            _logger.LogInformation("Retrieving total time leaderboard for {ActivityId}.", activityId);
            var leaderboard = await _queryService.GetTotalTimeLeaderboardAsync(activityId);
            return ResponseHelpers.CachedJson(req, leaderboard, _jsonOptions, 300);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving total time leaderboard for {ActivityId}.", activityId);
            return new StatusCodeResult(500);
        }
    }

    [Function(nameof(ComputeLeaderboards))]
    public async Task<IActionResult> ComputeLeaderboards([HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "activities/leaderboards/compute")] HttpRequest req)
    {
        try
        {
            var securityKey = Environment.GetEnvironmentVariable("SecurityKey:ComputeLeaderboard");
            if (!string.IsNullOrEmpty(securityKey) && req.Headers["x-security-key"].ToString() != securityKey)
            {
                _logger.LogWarning("Rejected leaderboard computation due to invalid security key header.");
                return new StatusCodeResult(401);
            }

            var activities = await _queryService.GetAllActivitiesAsync();
            var idList = activities.SelectMany(a => a.Activities.Select(a => a.Id)).ToList();
            idList.Add(0);
            foreach (var id in idList)
            {
                await _queryService.ComputeCompletionsLeaderboardAsync(id);
                await _queryService.ComputeSpeedLeaderboardAsync(id);
                await _queryService.ComputeTotalTimeLeaderboardAsync(id);
            }

            return new OkResult();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error computing leaderboards from manual request.");
            return new StatusCodeResult(500);
        }
    }

    [Function(nameof(ComputeLeaderboardsTimer))]
    public async Task ComputeLeaderboardsTimer([TimerTrigger("0 0 0 * * *")] TimerInfo timer)
    {
        try
        {
            _logger.LogInformation("Computing leaderboards (timer trigger).");
            var activities = await _queryService.GetAllActivitiesAsync();
            var idList = activities.SelectMany(a => a.Activities.Select(a => a.Id)).ToList();
            idList.Add(0);
            foreach (var id in idList)
            {
                await _queryService.ComputeCompletionsLeaderboardAsync(id);
                await _queryService.ComputeSpeedLeaderboardAsync(id);
                await _queryService.ComputeTotalTimeLeaderboardAsync(id);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error computing leaderboards from timer trigger.");
        }
    }
}
