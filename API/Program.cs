using API.Clients;
using API.Clients.Abstract;
using API.Services;
using API.Services.Abstract;
using Domain.Configuration;
using Domain.Data;
using Domain.Serializers;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using System.Linq;
using System.Text.Json;

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

builder.Services
    .AddApplicationInsightsTelemetryWorkerService()
    .ConfigureFunctionsApplicationInsights();

builder.Logging.Services.Configure<LoggerFilterOptions>(options =>
{
    var aiRule = options.Rules.FirstOrDefault(rule =>
        rule.ProviderName == "Microsoft.Extensions.Logging.ApplicationInsights.ApplicationInsightsLoggerProvider");
    if (aiRule is not null)
    {
        options.Rules.Remove(aiRule);
    }

    options.AddFilter("Microsoft.Extensions.Logging.ApplicationInsights.ApplicationInsightsLoggerProvider",
        LogLevel.Information);
});

builder.Logging.AddConsole();

if (builder.Environment.IsDevelopment())
{
    builder.Logging.AddFilter("Microsoft.Extensions.Logging.Console", LogLevel.Debug);
    builder.Logging.AddFilter("API.Functions.CrawlerFunctions", LogLevel.Error);
}
else
{
    builder.Logging.AddFilter("Microsoft.Extensions.Logging.Console", LogLevel.Warning);
}


builder.Services.AddDbContextPool<AppDbContext>(options =>
{
    options.UseNpgsql(builder.Configuration.GetConnectionString("PostgreSqlConnectionString"), npgsqlOptions =>
    {
        npgsqlOptions.EnableRetryOnFailure(
            maxRetryCount: 3,
            maxRetryDelay: TimeSpan.FromSeconds(30),
            errorCodesToAdd: null);
    });

    options.EnableServiceProviderCaching();
    options.EnableSensitiveDataLogging(builder.Environment.IsDevelopment());
}, poolSize: 64);


builder.Services.AddOptions<Destiny2Options>()
    .Configure<IConfiguration>((settings, configuration) =>
    {
        configuration.GetSection("Destiny2Api").Bind(settings);
    });

builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
    ConnectionMultiplexer.Connect(builder.Configuration.GetConnectionString("RedisConnectionString") ?? throw new InvalidOperationException("RedisConnectionString is not configured"))
);

builder.Services.AddHttpClient<IDestiny2ApiClient, Destiny2ApiClient>();
builder.Services.AddHttpClient<IManifestClient, ManifestClient>();
builder.Services.AddScoped<IDestiny2Service, Destiny2Service>();
builder.Services.AddScoped<IQueryService, QueryService>();

builder.Services.AddSingleton(sp =>
{
    var json = new JsonSerializerOptions(JsonSerializerDefaults.Web);
    json.Converters.Add(new Int64AsStringJsonConverter());
    return json;
});

builder.Build().Run();
