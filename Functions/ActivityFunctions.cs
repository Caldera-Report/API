using API.Helpers;
using API.Services.Abstract;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
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

    [Function("GetActivities")]
    public async Task<IActionResult> GetActivities([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "activities")] HttpRequest req)
    {
        try
        {
            var activities = await _queryService.GetAllActivitiesAsync();
            return ResponseHelpers.CachedJson(req, activities, _jsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving activities");
            return new StatusCodeResult(500);
        }
    }

    [Function("CacheActivities")]
    public async Task CacheActivities([TimerTrigger("0 0 0 * * *")] TimerInfo timer)
    {
        try
        {
            await _queryService.CacheAllActivitiesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error caching activities");
        }
    }

    [Function("GetCompletionsLeaderboard")]
    public async Task<IActionResult> GetCompletionsLeaderboard([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "activities/leaderboards/completions/{activityId}")] HttpRequest req, long activityId)
    {
        try
        {
            var leaderboard = await _queryService.GetCompletionsLeaderboardAsync(activityId);
            return ResponseHelpers.CachedJson(req, leaderboard, _jsonOptions, 300);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving completions leaderboard");
            return new StatusCodeResult(500);
        }
    }

    [Function("GetSpeedLeaderboard")]
    public async Task<IActionResult> GetSpeedLeaderboard([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "activities/leaderboards/speed/{activityId}")] HttpRequest req, long activityId)
    {
        try
        {
            var leaderboard = await _queryService.GetSpeedLeaderboardAsync(activityId);
            return ResponseHelpers.CachedJson(req, leaderboard, _jsonOptions, 300);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving speed leaderboard");
            return new StatusCodeResult(500);
        }
    }

    [Function("GetTotalTimeLeaderboard")]
    public async Task<IActionResult> GetTotalTimeLeaderboard([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "activities/leaderboards/totalTime/{activityId}")] HttpRequest req, long activityId)
    {
        try
        {
            var leaderboard = await _queryService.GetTotalTimeLeaderboardAsync(activityId);
            return ResponseHelpers.CachedJson(req, leaderboard, _jsonOptions, 300);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving total time leaderboard");
            return new StatusCodeResult(500);
        }
    }

    [Function("ComputeLeaderboards")]
    public async Task ComputeLeaderboards([TimerTrigger("0 0 0 * * *")] TimerInfo timer)
    {
        try
        {
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
            _logger.LogError(ex, "Error computing leaderboards");
        }
    }
}