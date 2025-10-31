extern alias APIAssembly;
using API.Domain.DTO.Responses;
using API.Models.Responses;
using APIAssembly::API.Functions;
using APIAssembly::API.Services.Abstract;
using Domain.DB;
using Domain.DestinyApi;
using Domain.DTO.Requests;
using Domain.DTO.Responses;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using System.Text.Json;
using It = Moq.It;

namespace API.Tests.Functions;

public class PlayerFunctionsTests
{
    private readonly Mock<IDestiny2Service> _destiny2Service = new();
    private readonly Mock<IQueryService> _queryService = new();
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);
    private readonly PlayerFunctions _functions;

    public PlayerFunctionsTests()
    {
        var logger = Mock.Of<ILogger<PlayerFunctions>>();
        _functions = new PlayerFunctions(logger, _destiny2Service.Object, _queryService.Object, _jsonOptions);
    }

    [Fact]
    public async Task SearchForPlayer_ReturnsBadRequest_WhenNameMissing()
    {
        var request = new SearchRequest { playerName = string.Empty };

        var result = await _functions.SearchForPlayer(new DefaultHttpContext().Request, request);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Player name is required", badRequest.Value);
    }

    [Fact]
    public async Task SearchForPlayer_ReturnsResults_WhenServiceSucceeds()
    {
        var request = new SearchRequest { playerName = "Test" };
        var results = new List<PlayerSearchDto> { new() { FullDisplayName = "Tester" } };
        _destiny2Service.Setup(s => s.SearchForPlayer("Test")).ReturnsAsync(results);

        var httpContext = new DefaultHttpContext();
        var result = await _functions.SearchForPlayer(httpContext.Request, request);

        var content = Assert.IsType<ContentResult>(result);
        Assert.Equal(StatusCodes.Status200OK, content.StatusCode);
        Assert.Equal("application/json", content.ContentType);
        Assert.Equal(JsonSerializer.Serialize(results, _jsonOptions), content.Content);
    }

    [Fact]
    public async Task SearchForPlayer_ReturnsServerError_OnException()
    {
        var request = new SearchRequest { playerName = "Error" };
        _destiny2Service.Setup(s => s.SearchForPlayer("Error")).ThrowsAsync(new Exception());

        var result = await _functions.SearchForPlayer(new DefaultHttpContext().Request, request);

        var status = Assert.IsType<StatusCodeResult>(result);
        Assert.Equal(StatusCodes.Status500InternalServerError, status.StatusCode);
    }

    [Fact]
    public async Task GetPlayer_ReturnsBadRequest_WhenMembershipIdInvalid()
    {
        var result = await _functions.GetPlayer(new DefaultHttpContext().Request, 0);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Membership ID and type are required", badRequest.Value);
    }

    [Fact]
    public async Task GetPlayer_ReturnsCachedJson_WhenServiceSucceeds()
    {
        var player = new PlayerDto { FullDisplayName = "Tester" };
        _queryService.Setup(q => q.GetPlayerAsync(123)).ReturnsAsync(player);
        var context = new DefaultHttpContext();

        var result = await _functions.GetPlayer(context.Request, 123);

        var content = Assert.IsType<ContentResult>(result);
        Assert.Equal(StatusCodes.Status200OK, content.StatusCode);
        Assert.Equal(JsonSerializer.Serialize(player, _jsonOptions), content.Content);
        Assert.True(string.IsNullOrEmpty(context.Response.Headers.CacheControl));
    }

    [Fact]
    public async Task GetPlayer_ReturnsServerError_OnException()
    {
        _queryService.Setup(q => q.GetPlayerAsync(1)).ThrowsAsync(new Exception());

        var result = await _functions.GetPlayer(new DefaultHttpContext().Request, 1);

        var status = Assert.IsType<StatusCodeResult>(result);
        Assert.Equal(StatusCodes.Status500InternalServerError, status.StatusCode);
    }

    [Fact]
    public async Task GetAllPlayers_ReturnsCachedJson()
    {
        var players = new List<PlayerSearchDto> { new() { FullDisplayName = "Tester" } };
        _queryService.Setup(q => q.GetAllPlayersAsync()).ReturnsAsync(players);
        var context = new DefaultHttpContext();

        var result = await _functions.GetAllPlayers(context.Request);

        var content = Assert.IsType<ContentResult>(result);
        Assert.Equal(StatusCodes.Status200OK, content.StatusCode);
        Assert.Equal(JsonSerializer.Serialize(players, _jsonOptions), content.Content);
        Assert.True(string.IsNullOrEmpty(context.Response.Headers.CacheControl));
    }

    [Fact]
    public async Task GetAllPlayers_ReturnsServerError_OnException()
    {
        _queryService.Setup(q => q.GetAllPlayersAsync()).ThrowsAsync(new Exception());

        var result = await _functions.GetAllPlayers(new DefaultHttpContext().Request);

        var status = Assert.IsType<StatusCodeResult>(result);
        Assert.Equal(StatusCodes.Status500InternalServerError, status.StatusCode);
    }

    [Fact]
    public async Task GetPlayerStatsForActivity_ReturnsCachedJson()
    {
        var reports = new ActivityReportListDTO
        {
            Reports = new List<ActivityReportPlayerFacet> { new() }
        };
        _queryService.Setup(q => q.GetPlayerReportsForActivityAsync(1, 2)).ReturnsAsync(reports);
        var context = new DefaultHttpContext();

        var result = await _functions.GetPlayerStatsForActivity(context.Request, 1, 2);

        var content = Assert.IsType<ContentResult>(result);
        Assert.Equal(StatusCodes.Status200OK, content.StatusCode);
        Assert.Equal(JsonSerializer.Serialize(reports, _jsonOptions), content.Content);
        Assert.Equal("public, max-age=300", context.Response.Headers.CacheControl.ToString());
    }

    [Fact]
    public async Task GetPlayerStatsForActivity_ReturnsServerError_OnException()
    {
        _queryService.Setup(q => q.GetPlayerReportsForActivityAsync(1, 2)).ThrowsAsync(new Exception());

        var result = await _functions.GetPlayerStatsForActivity(new DefaultHttpContext().Request, 1, 2);

        var status = Assert.IsType<StatusCodeResult>(result);
        Assert.Equal(StatusCodes.Status500InternalServerError, status.StatusCode);
    }

    [Fact]
    public async Task LoadPlayerActivities_ReturnsBadRequest_WhenMembershipIdInvalid()
    {
        var result = await _functions.LoadPlayerActivities(new DefaultHttpContext().Request, 0);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Membership ID and type are required", badRequest.Value);
    }

    [Fact]
    public async Task LoadPlayerActivities_ReturnsNoContent_WhenUpdatedRecently()
    {
        var player = new Player
        {
            Id = 15,
            DisplayName = "Tester",
            DisplayNameCode = 123,
            LastUpdateCompleted = DateTime.UtcNow,
            MembershipType = 1
        };
        _queryService.Setup(q => q.GetPlayerDbObject(15)).ReturnsAsync(player);

        var result = await _functions.LoadPlayerActivities(new DefaultHttpContext().Request, 15);

        Assert.IsType<NoContentResult>(result);
        _destiny2Service.Verify(s => s.GetCharactersForPlayer(Moq.It.IsAny<long>(), Moq.It.IsAny<int>()), Times.Never);
        _destiny2Service.Verify(s => s.LoadPlayerActivityReports(Moq.It.IsAny<Player>(), It.IsAny<DateTime>(), It.IsAny<string>()), Times.Never);
        _queryService.Verify(q => q.UpdatePlayerEmblems(Moq.It.IsAny<Player>(), Moq.It.IsAny<string>(), Moq.It.IsAny<string>()), Times.Never);
        _queryService.Verify(q => q.GetPlayerLastPlayedActivityDate(It.IsAny<long>()), Times.Never);
    }

    [Fact]
    public async Task LoadPlayerActivities_LoadsActivitiesAndUpdatesEmblems()
    {
        var player = new Player
        {
            Id = 20,
            DisplayName = "Tester",
            DisplayNameCode = 123,
            LastUpdateCompleted = DateTime.UtcNow.AddHours(-1),
            MembershipType = 2
        };
        _queryService.Setup(q => q.GetPlayerDbObject(20)).ReturnsAsync(player);
        var lastPlayedActivity = DateTime.UtcNow.AddDays(-1);
        _queryService.Setup(q => q.GetPlayerLastPlayedActivityDate(20)).ReturnsAsync(lastPlayedActivity);

        var older = new DestinyCharacterComponent
        {
            dateLastPlayed = DateTime.UtcNow.AddHours(-5),
            emblemBackgroundPath = "bg-old",
            emblemPath = "emblem-old"
        };
        var newer = new DestinyCharacterComponent
        {
            dateLastPlayed = DateTime.UtcNow,
            emblemBackgroundPath = "bg-new",
            emblemPath = "emblem-new"
        };
        var characters = new Dictionary<string, DestinyCharacterComponent>
        {
            ["111"] = older,
            ["222"] = newer
        };
        _destiny2Service.Setup(s => s.GetCharactersForPlayer(20, 2)).ReturnsAsync(characters);
        _destiny2Service.Setup(s => s.LoadPlayerActivityReports(player, It.IsAny<DateTime>(), It.IsAny<string>())).Returns(Task.CompletedTask);
        _queryService.Setup(q => q.UpdatePlayerEmblems(player, Moq.It.IsAny<string>(), Moq.It.IsAny<string>())).Returns(Task.CompletedTask);

        var result = await _functions.LoadPlayerActivities(new DefaultHttpContext().Request, 20);

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equivalent(new { Success = true }, okResult.Value);
        _destiny2Service.Verify(s => s.GetCharactersForPlayer(20, 2), Times.Once);
        _queryService.Verify(q => q.GetPlayerLastPlayedActivityDate(20), Times.Once);
        _destiny2Service.Verify(s => s.LoadPlayerActivityReports(player, lastPlayedActivity, "111"), Times.Once);
        _destiny2Service.Verify(s => s.LoadPlayerActivityReports(player, lastPlayedActivity, "222"), Times.Once);
        _queryService.Verify(q => q.UpdatePlayerEmblems(player, "bg-new", "emblem-new"), Times.Once);
    }

    [Fact]
    public async Task LoadPlayerActivities_ReturnsServerError_OnException()
    {
        _queryService.Setup(q => q.GetPlayerDbObject(33)).ThrowsAsync(new Exception());

        var result = await _functions.LoadPlayerActivities(new DefaultHttpContext().Request, 33);

        var status = Assert.IsType<StatusCodeResult>(result);
        Assert.Equal(StatusCodes.Status500InternalServerError, status.StatusCode);
    }
}
