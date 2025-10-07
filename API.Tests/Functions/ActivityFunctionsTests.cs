extern alias APIAssembly;

using APIAssembly::API.Functions;
using APIAssembly::API.Models.Responses;
using APIAssembly::API.Services.Abstract;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using System.Text.Json;

namespace API.Tests.Functions;

public class ActivityFunctionsTests
{
    private readonly Mock<IQueryService> _queryService = new();
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);
    private readonly ActivityFunctions _functions;

    public ActivityFunctionsTests()
    {
        var logger = Mock.Of<ILogger<ActivityFunctions>>();
        _functions = new ActivityFunctions(_queryService.Object, logger, _jsonOptions);
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
    public async Task GetCompletionsLeaderboard_ReturnsCachedJson()
    {
        var leaderboard = new List<CompletionsLeaderboardResponse>
        {
            new() { Player = new PlayerDto { FullDisplayName = "Tester" }, Completions = 5 }
        };
        _queryService.Setup(q => q.GetCompletionsLeaderboardAsync(42)).ReturnsAsync(leaderboard);
        var context = new DefaultHttpContext();

        var result = await _functions.GetCompletionsLeaderboard(context.Request, 42);

        var content = Assert.IsType<ContentResult>(result);
        Assert.Equal(StatusCodes.Status200OK, content.StatusCode);
        Assert.Equal(JsonSerializer.Serialize(leaderboard, _jsonOptions), content.Content);
        Assert.Equal("public, max-age=300", context.Response.Headers.CacheControl.ToString());
        _queryService.Verify(q => q.GetCompletionsLeaderboardAsync(42), Times.Once);
    }

    [Fact]
    public async Task GetCompletionsLeaderboard_ReturnsServerError_OnException()
    {
        _queryService.Setup(q => q.GetCompletionsLeaderboardAsync(42)).ThrowsAsync(new Exception());

        var result = await _functions.GetCompletionsLeaderboard(new DefaultHttpContext().Request, 42);

        var status = Assert.IsType<StatusCodeResult>(result);
        Assert.Equal(StatusCodes.Status500InternalServerError, status.StatusCode);
    }

    [Fact]
    public async Task GetSpeedLeaderboard_ReturnsCachedJson()
    {
        var leaderboard = new List<TimeLeaderboardResponse>
        {
            new() { Player = new PlayerDto { FullDisplayName = "Speedster" }, Time = TimeSpan.FromMinutes(10) }
        };
        _queryService.Setup(q => q.GetSpeedLeaderboardAsync(7)).ReturnsAsync(leaderboard);
        var context = new DefaultHttpContext();

        var result = await _functions.GetSpeedLeaderboard(context.Request, 7);

        var content = Assert.IsType<ContentResult>(result);
        Assert.Equal(StatusCodes.Status200OK, content.StatusCode);
        Assert.Equal(JsonSerializer.Serialize(leaderboard, _jsonOptions), content.Content);
        Assert.Equal("public, max-age=300", context.Response.Headers.CacheControl.ToString());
        _queryService.Verify(q => q.GetSpeedLeaderboardAsync(7), Times.Once);
    }

    [Fact]
    public async Task GetSpeedLeaderboard_ReturnsServerError_OnException()
    {
        _queryService.Setup(q => q.GetSpeedLeaderboardAsync(1)).ThrowsAsync(new Exception());

        var result = await _functions.GetSpeedLeaderboard(new DefaultHttpContext().Request, 1);

        var status = Assert.IsType<StatusCodeResult>(result);
        Assert.Equal(StatusCodes.Status500InternalServerError, status.StatusCode);
    }

    [Fact]
    public async Task GetTotalTimeLeaderboard_ReturnsCachedJson()
    {
        var leaderboard = new List<TimeLeaderboardResponse>
        {
            new() { Player = new PlayerDto { FullDisplayName = "Marathoner" }, Time = TimeSpan.FromHours(2) }
        };
        _queryService.Setup(q => q.GetTotalTimeLeaderboardAsync(9)).ReturnsAsync(leaderboard);
        var context = new DefaultHttpContext();

        var result = await _functions.GetTotalTimeLeaderboard(context.Request, 9);

        var content = Assert.IsType<ContentResult>(result);
        Assert.Equal(StatusCodes.Status200OK, content.StatusCode);
        Assert.Equal(JsonSerializer.Serialize(leaderboard, _jsonOptions), content.Content);
        Assert.Equal("public, max-age=300", context.Response.Headers.CacheControl.ToString());
        _queryService.Verify(q => q.GetTotalTimeLeaderboardAsync(9), Times.Once);
    }

    [Fact]
    public async Task GetTotalTimeLeaderboard_ReturnsServerError_OnException()
    {
        _queryService.Setup(q => q.GetTotalTimeLeaderboardAsync(9)).ThrowsAsync(new Exception());

        var result = await _functions.GetTotalTimeLeaderboard(new DefaultHttpContext().Request, 9);

        var status = Assert.IsType<StatusCodeResult>(result);
        Assert.Equal(StatusCodes.Status500InternalServerError, status.StatusCode);
    }

    [Fact]
    public async Task ComputeLeaderboards_ProcessesAllIds()
    {
        var activities = new List<OpTypeDto>
        {
            new()
            {
                Activities = new[]
                {
                    new ActivityDto { Id = 11 },
                    new ActivityDto { Id = 22 }
                }
            }
        };
        _queryService.Setup(q => q.GetAllActivitiesAsync()).ReturnsAsync(activities);

        await _functions.ComputeLeaderboards(default!);

        _queryService.Verify(q => q.ComputeCompletionsLeaderboardAsync(11), Times.Once);
        _queryService.Verify(q => q.ComputeSpeedLeaderboardAsync(11), Times.Once);
        _queryService.Verify(q => q.ComputeTotalTimeLeaderboardAsync(11), Times.Once);
        _queryService.Verify(q => q.ComputeCompletionsLeaderboardAsync(22), Times.Once);
        _queryService.Verify(q => q.ComputeSpeedLeaderboardAsync(22), Times.Once);
        _queryService.Verify(q => q.ComputeTotalTimeLeaderboardAsync(22), Times.Once);
        _queryService.Verify(q => q.ComputeCompletionsLeaderboardAsync(0), Times.Once);
        _queryService.Verify(q => q.ComputeSpeedLeaderboardAsync(0), Times.Once);
        _queryService.Verify(q => q.ComputeTotalTimeLeaderboardAsync(0), Times.Once);
    }

    [Fact]
    public async Task ComputeLeaderboards_SwallowsExceptions()
    {
        _queryService.Setup(q => q.GetAllActivitiesAsync()).ThrowsAsync(new Exception());

        await _functions.ComputeLeaderboards(default!);
    }
}
