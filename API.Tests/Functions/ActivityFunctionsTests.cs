extern alias APIAssembly;
using API.Models.Responses;
using APIAssembly::API.Functions;
using APIAssembly::API.Services.Abstract;
using Domain.DTO.Responses;
using Domain.Enums;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using System.Text.Json;

namespace API.Tests.Functions;

public class ActivityFunctionsTests
{
    private readonly Mock<IQueryService> _queryService = new();
    private readonly Mock<IDestiny2Service> _destiny2Service = new();
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);
    private readonly ActivityFunctions _functions;

    public ActivityFunctionsTests()
    {
        var logger = Mock.Of<ILogger<ActivityFunctions>>();
        _functions = new ActivityFunctions(_queryService.Object, _destiny2Service.Object, logger, _jsonOptions);
    }

    [Fact]
    public async Task GetActivities_ReturnsCachedJson_OnSuccess()
    {
        var activities = new List<OpTypeDto>
        {
            new() { Activities = Array.Empty<ActivityDto>() }
        };
        _queryService.Setup(q => q.GetAllActivitiesAsync()).ReturnsAsync(activities);
        var context = new DefaultHttpContext();

        var result = await _functions.GetActivities(context.Request);

        var content = Assert.IsType<ContentResult>(result);
        Assert.Equal(StatusCodes.Status200OK, content.StatusCode);
        Assert.Equal(JsonSerializer.Serialize(activities, _jsonOptions), content.Content);
        Assert.Equal("public, max-age=3600", context.Response.Headers.CacheControl.ToString());
    }

    [Fact]
    public async Task GetActivities_ReturnsServerError_OnException()
    {
        _queryService.Setup(q => q.GetAllActivitiesAsync()).ThrowsAsync(new InvalidOperationException());

        var result = await _functions.GetActivities(new DefaultHttpContext().Request);

        var status = Assert.IsType<StatusCodeResult>(result);
        Assert.Equal(StatusCodes.Status500InternalServerError, status.StatusCode);
    }

    [Fact]
    public async Task CacheActivities_InvokesService()
    {
        _queryService.Setup(q => q.CacheAllActivitiesAsync()).Returns(Task.CompletedTask);

        await _functions.CacheActivities(default!);

        _queryService.Verify(q => q.CacheAllActivitiesAsync(), Times.Once);
    }

    [Fact]
    public async Task CacheActivities_SwallowsExceptions()
    {
        _queryService.Setup(q => q.CacheAllActivitiesAsync()).ThrowsAsync(new Exception());

        await _functions.CacheActivities(default!);
    }

    [Fact]
    public async Task GetLeaderboard_CompletionsType_ReturnsCachedJson()
    {
        var leaderboard = new List<LeaderboardResponse>
        {
            new() { Player = new PlayerDto { FullDisplayName = "Tester" }, Rank = 1, Data = "5" }
        };
        _queryService.Setup(q => q.GetLeaderboardAsync(42, LeaderboardTypes.TotalCompletions, 250, 0))
                     .ReturnsAsync(leaderboard);
        var context = new DefaultHttpContext();

        var result = await _functions.GetLeaderboard(context.Request, "completions", 42);

        var content = Assert.IsType<ContentResult>(result);
        Assert.Equal(StatusCodes.Status200OK, content.StatusCode);
        Assert.Equal(JsonSerializer.Serialize(leaderboard, _jsonOptions), content.Content);
        Assert.Equal("public, max-age=300", context.Response.Headers.CacheControl.ToString());
        _queryService.Verify(q => q.GetLeaderboardAsync(42, LeaderboardTypes.TotalCompletions, 250, 0), Times.Once);
    }

    [Fact]
    public async Task GetLeaderboard_SpeedType_ReturnsCachedJson()
    {
        var leaderboard = new List<LeaderboardResponse>
        {
            new() { Player = new PlayerDto { FullDisplayName = "Speedster" }, Rank = 1, Data = TimeSpan.FromMinutes(10).ToString() }
        };
        _queryService.Setup(q => q.GetLeaderboardAsync(7, LeaderboardTypes.FastestCompletion, 250, 0))
                     .ReturnsAsync(leaderboard);
        var context = new DefaultHttpContext();

        var result = await _functions.GetLeaderboard(context.Request, "speed", 7);

        var content = Assert.IsType<ContentResult>(result);
        Assert.Equal(StatusCodes.Status200OK, content.StatusCode);
        Assert.Equal(JsonSerializer.Serialize(leaderboard, _jsonOptions), content.Content);
        Assert.Equal("public, max-age=300", context.Response.Headers.CacheControl.ToString());
        _queryService.Verify(q => q.GetLeaderboardAsync(7, LeaderboardTypes.FastestCompletion, 250, 0), Times.Once);
    }

    [Fact]
    public async Task GetLeaderboard_DefaultsToHighestScoreForUnknownType()
    {
        var leaderboard = new List<LeaderboardResponse>
        {
            new() { Player = new PlayerDto { FullDisplayName = "Marathoner" }, Rank = 1, Data = "12345" }
        };
        _queryService.Setup(q => q.GetLeaderboardAsync(9, LeaderboardTypes.HighestScore, 250, 0))
                     .ReturnsAsync(leaderboard);
        var context = new DefaultHttpContext();

        var result = await _functions.GetLeaderboard(context.Request, "unknown", 9);

        var content = Assert.IsType<ContentResult>(result);
        Assert.Equal(StatusCodes.Status200OK, content.StatusCode);
        Assert.Equal(JsonSerializer.Serialize(leaderboard, _jsonOptions), content.Content);
        Assert.Equal("public, max-age=300", context.Response.Headers.CacheControl.ToString());
        _queryService.Verify(q => q.GetLeaderboardAsync(9, LeaderboardTypes.HighestScore, 250, 0), Times.Once);
    }

    [Fact]
    public async Task GetLeaderboard_HonorsQueryParameters()
    {
        var leaderboard = new List<LeaderboardResponse>();
        _queryService.Setup(q => q.GetLeaderboardAsync(5, LeaderboardTypes.TotalCompletions, 100, 50))
                     .ReturnsAsync(leaderboard);
        var context = new DefaultHttpContext();
        context.Request.QueryString = new QueryString("?count=100&offset=50");

        var result = await _functions.GetLeaderboard(context.Request, "completions", 5);

        _ = Assert.IsType<ContentResult>(result);
        _queryService.Verify(q => q.GetLeaderboardAsync(5, LeaderboardTypes.TotalCompletions, 100, 50), Times.Once);
    }

    [Fact]
    public async Task GetLeaderboard_ReturnsServerError_OnException()
    {
        _queryService.Setup(q => q.GetLeaderboardAsync(1, LeaderboardTypes.HighestScore, 250, 0))
                     .ThrowsAsync(new Exception());

        var result = await _functions.GetLeaderboard(new DefaultHttpContext().Request, "score", 1);

        var status = Assert.IsType<StatusCodeResult>(result);
        Assert.Equal(StatusCodes.Status500InternalServerError, status.StatusCode);
    }
}
