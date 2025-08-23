using API.Services.Abstract;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace API.Functions;

public class PlayerLoad
{
    private readonly ILogger<PlayerLoad> _logger;
    private readonly IDestiny2Service _d2Service;

    public PlayerLoad(ILogger<PlayerLoad> logger, IDestiny2Service d2Service)
    {
        _logger = logger;
        _d2Service = d2Service;
    }

    [Function("PlayerLoad")]
    public async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "playerload")] HttpRequest req)
    {
        _logger.LogInformation("C# HTTP trigger function processed a request.");
        var players = await _d2Service.GetAllPlayers();
        return new OkObjectResult(players);
    }
}