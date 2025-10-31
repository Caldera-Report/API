extern alias APIAssembly;
using API.Models.Responses;
using APIAssembly::API.Functions;
using APIAssembly::API.Services.Abstract;
using Domain.DTO.Responses;
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
        _queryService.Setup(q => q.ComputeCompletionsLeaderboardAsync(It.IsAny<long>())).Returns(Task.CompletedTask);
        _queryService.Setup(q => q.ComputeSpeedLeaderboardAsync(It.IsAny<long>())).Returns(Task.CompletedTask);
        _queryService.Setup(q => q.ComputeTotalTimeLeaderboardAsync(It.IsAny<long>())).Returns(Task.CompletedTask);

        var context = new DefaultHttpContext();

        var result = await _functions.ComputeLeaderboards(context.Request);

        Assert.IsType<OkResult>(result);
        _queryService.Verify(q => q.ComputeCompletionsLeaderboardAsync(11), Times.Once);
        _queryService.Verify(q => q.ComputeSpeedLeaderboardAsync(11), Times.Once);
        _queryService.Verify(q => q.ComputeTotalTimeLeaderboardAsync(11), Times.Once);
        _queryService.Verify(q => q.ComputeCompletionsLeaderboardAsync(22), Times.Once);
        _queryService.Verify(q => q.ComputeSpeedLeaderboardAsync(22), Times.Once);
        _queryService.Verify(q => q.ComputeTotalTimeLeaderboardAsync(22), Times.Once);
    }

    [Fact]
    public async Task ComputeLeaderboards_ReturnsUnauthorized_WhenSecurityKeyMismatch()
    {
        using var _ = UseEnvironmentVariable("SecurityKey:ComputeLeaderboard", "expected");
        var context = new DefaultHttpContext();
        context.Request.Headers["x-security-key"] = "wrong";

        var result = await _functions.ComputeLeaderboards(context.Request);

        var status = Assert.IsType<StatusCodeResult>(result);
        Assert.Equal(StatusCodes.Status401Unauthorized, status.StatusCode);
        _queryService.Verify(q => q.GetAllActivitiesAsync(), Times.Never);
    }

    [Fact]
    public async Task ComputeLeaderboards_ReturnsOk_WhenSecurityKeyMatches()
    {
        var activities = new List<OpTypeDto>
        {
            new()
            {
                Activities = new[]
                {
                    new ActivityDto { Id = 31 }
                }
            }
        };
        _queryService.Setup(q => q.GetAllActivitiesAsync()).ReturnsAsync(activities);
        _queryService.Setup(q => q.ComputeCompletionsLeaderboardAsync(It.IsAny<long>())).Returns(Task.CompletedTask);
        _queryService.Setup(q => q.ComputeSpeedLeaderboardAsync(It.IsAny<long>())).Returns(Task.CompletedTask);
        _queryService.Setup(q => q.ComputeTotalTimeLeaderboardAsync(It.IsAny<long>())).Returns(Task.CompletedTask);

        using var _ = UseEnvironmentVariable("SecurityKey:ComputeLeaderboard", "secret");
        var context = new DefaultHttpContext();
        context.Request.Headers["x-security-key"] = "secret";

        var result = await _functions.ComputeLeaderboards(context.Request);

        Assert.IsType<OkResult>(result);
        _queryService.Verify(q => q.GetAllActivitiesAsync(), Times.Once);
        _queryService.Verify(q => q.ComputeCompletionsLeaderboardAsync(31), Times.Once);
        _queryService.Verify(q => q.ComputeSpeedLeaderboardAsync(31), Times.Once);
        _queryService.Verify(q => q.ComputeTotalTimeLeaderboardAsync(31), Times.Once);
    }

    [Fact]
    public async Task ComputeLeaderboards_ReturnsServerError_OnException()
    {
        _queryService.Setup(q => q.GetAllActivitiesAsync()).ThrowsAsync(new Exception());
        var context = new DefaultHttpContext();

        var result = await _functions.ComputeLeaderboards(context.Request);

        var status = Assert.IsType<StatusCodeResult>(result);
        Assert.Equal(StatusCodes.Status500InternalServerError, status.StatusCode);
    }

    [Fact]
    public async Task ComputeLeaderboardsTimer_ProcessesAllIds()
    {
        var activities = new List<OpTypeDto>
        {
            new()
            {
                Activities = new[]
                {
                    new ActivityDto { Id = 99 },
                    new ActivityDto { Id = 100 }
                }
            }
        };
        _queryService.Setup(q => q.GetAllActivitiesAsync()).ReturnsAsync(activities);
        _queryService.Setup(q => q.ComputeCompletionsLeaderboardAsync(It.IsAny<long>())).Returns(Task.CompletedTask);
        _queryService.Setup(q => q.ComputeSpeedLeaderboardAsync(It.IsAny<long>())).Returns(Task.CompletedTask);
        _queryService.Setup(q => q.ComputeTotalTimeLeaderboardAsync(It.IsAny<long>())).Returns(Task.CompletedTask);

        await _functions.ComputeLeaderboardsTimer(default!);

        _queryService.Verify(q => q.ComputeCompletionsLeaderboardAsync(99), Times.Once);
        _queryService.Verify(q => q.ComputeSpeedLeaderboardAsync(99), Times.Once);
        _queryService.Verify(q => q.ComputeTotalTimeLeaderboardAsync(99), Times.Once);
        _queryService.Verify(q => q.ComputeCompletionsLeaderboardAsync(100), Times.Once);
        _queryService.Verify(q => q.ComputeSpeedLeaderboardAsync(100), Times.Once);
        _queryService.Verify(q => q.ComputeTotalTimeLeaderboardAsync(100), Times.Once);
    }

    [Fact]
    public async Task ComputeLeaderboardsTimer_SwallowsExceptions()
    {
        _queryService.Setup(q => q.GetAllActivitiesAsync()).ThrowsAsync(new Exception());

        await _functions.ComputeLeaderboardsTimer(default!);
    }

    private static IDisposable UseEnvironmentVariable(string name, string? value)
    {
        var original = Environment.GetEnvironmentVariable(name);
        Environment.SetEnvironmentVariable(name, value);
        return new EnvironmentVariableScope(name, original);
    }

    private sealed class EnvironmentVariableScope : IDisposable
    {
        private readonly string _name;
        private readonly string? _value;

        public EnvironmentVariableScope(string name, string? value)
        {
            _name = name;
            _value = value;
        }

        public void Dispose() => Environment.SetEnvironmentVariable(_name, _value);
    }
}
