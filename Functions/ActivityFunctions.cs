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
}