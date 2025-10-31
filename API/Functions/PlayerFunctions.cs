using API.Helpers;
using API.Services.Abstract;
using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.AppContainers;
using Domain.DTO.Requests;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using System.Linq;
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

    [Function(nameof(SearchForPlayer))]
    public async Task<IActionResult> SearchForPlayer([HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "players/search")] HttpRequest req, [FromBody] SearchRequest request)
    {
        var playerName = request.playerName;
        _logger.LogInformation("Search request received for player {PlayerName}.", playerName);

        if (string.IsNullOrEmpty(playerName))
        {
            _logger.LogWarning("Search request rejected due to missing player name.");
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
            _logger.LogError(ex, "Error searching for player {PlayerName}.", playerName);
            return new StatusCodeResult(500);
        }
    }

    [Function(nameof(GetPlayer))]
    public async Task<IActionResult> GetPlayer([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "players/{membershipId}")] HttpRequest req, long membershipId)
    {
        _logger.LogInformation("Player details requested for membership {MembershipId}.", membershipId);

        if (membershipId <= 0)
        {
            _logger.LogWarning("Player request rejected because membership ID {MembershipId} is invalid.", membershipId);
            return new BadRequestObjectResult("Membership ID and type are required");
        }

        try
        {
            var results = await _queryService.GetPlayerAsync(membershipId);
            return ResponseHelpers.CachedJson(req, results, _jsonOptions, 0);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving player {MembershipId}.", membershipId);
            return new StatusCodeResult(500);
        }
    }

    [Function(nameof(GetAllPlayers))]
    public async Task<IActionResult> GetAllPlayers([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "players")] HttpRequest req)
    {
        _logger.LogInformation("Processing request for all players.");

        try
        {
            var players = await _queryService.GetAllPlayersAsync();
            return ResponseHelpers.CachedJson(req, players, _jsonOptions, 0);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving all players.");
            return new StatusCodeResult(500);
        }
    }

    [Function(nameof(GetPlayerStatsForActivity))]
    public async Task<IActionResult> GetPlayerStatsForActivity([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "players/{membershipId}/stats/{activityId}")] HttpRequest req, long membershipId, long activityId)
    {
        _logger.LogInformation("Stats request received for player {MembershipId} and activity {ActivityId}.", membershipId, activityId);

        try
        {
            var reports = await _queryService.GetPlayerReportsForActivityAsync(membershipId, activityId);
            return ResponseHelpers.CachedJson(req, reports, _jsonOptions, 300);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting stats for player {MembershipId} and activity {ActivityId}.", membershipId, activityId);
            return new StatusCodeResult(500);
        }
    }

    [Function(nameof(LoadPlayerActivities))]
    public async Task<IActionResult> LoadPlayerActivities([HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "players/{membershipId}/load")] HttpRequest req, long membershipId)
    {
        _logger.LogInformation("Activities load requested for player {MembershipId}.", membershipId);

        if (membershipId <= 0)
        {
            _logger.LogWarning("Activities load rejected because membership ID {MembershipId} is invalid.", membershipId);
            return new BadRequestObjectResult("Membership ID and type are required");
        }
        try
        {
            var player = await _queryService.GetPlayerDbObject(membershipId);

            if (DateTime.UtcNow - player.LastUpdateCompleted < TimeSpan.FromMinutes(5))
            {
                _logger.LogInformation("Skipping activity load for {MembershipId} due to recent update.", membershipId);
                return new NoContentResult();
            }

            var characters = await _destiny2Service.GetCharactersForPlayer(membershipId, player.MembershipType);
            var lastPlayed = await _queryService.GetPlayerLastPlayedActivityDate(membershipId);
            foreach (var character in characters)
            {
                _logger.LogInformation("Loading activities for player {MembershipId} character {CharacterId}.", membershipId, character.Key);
                await _destiny2Service.LoadPlayerActivityReports(player, lastPlayed, character.Key);
            }

            var lastplayed = characters.Values.OrderByDescending(c => c.dateLastPlayed).FirstOrDefault();

            await _queryService.UpdatePlayerEmblems(player, lastplayed?.emblemBackgroundPath ?? "", lastplayed?.emblemPath ?? "");

            return new OkObjectResult(new { Success = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading activities for player {MembershipId}.", membershipId);
            return new StatusCodeResult(500);
        }
    }

    [Function(nameof(TriggerCrawler))]
    public async Task TriggerCrawler([TimerTrigger("0 0 0 * * *")] TimerInfo timer)
    {
        try
        {
            await _queryService.LoadPlayersQueue();
            await _destiny2Service.GroupActivityDuplicates();

            //Azure job containers aren't going to work out at least for the first load, so commenting this out for now.

            //var credential = new DefaultAzureCredential();

            //var armClient = new ArmClient(credential);

            //var subscription = Environment.GetEnvironmentVariable("AZURE_SUBSCRIPTION_ID");
            //string resourceGroupName = "Caldera-ReportResourceGroup";
            //string jobName = "caldera-report-crawler";

            //var jobResourceId = ContainerAppJobResource.CreateResourceIdentifier(subscription!, resourceGroupName, jobName);
            //var job = armClient.GetContainerAppJobResource(jobResourceId);

            //var executions = job.GetContainerAppJobExecutions().ToList();

            //bool isRunning = executions.Any(e =>
            //{
            //    var status = e.Data.Status;
            //    return status == "Running" || status == "Pending";
            //});

            //if (isRunning)
            //{
            //    _logger.LogWarning("A crawler job is already running, skipping new start.");
            //    return;
            //}

            //_logger.LogInformation("Starting crawler job...");
            //await job.StartAsync(Azure.WaitUntil.Started);

            _logger.LogInformation("Crawler job started successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error triggering player crawler job.");
        }
    }
}
