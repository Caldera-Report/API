using API.Clients;
using API.Clients.Abstract;
using API.Configuration;
using API.Data;
using API.Models.Serializers;
using API.Services;
using API.Services.Abstract;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Text.Json;

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

builder.Services.AddLogging(lo =>
{
    lo.AddSentry(o =>
    {
        o.Dsn = builder.Configuration["SentryDsn"];
        o.Environment = builder.Environment.EnvironmentName;
    });
});

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
    options.EnableSensitiveDataLogging(true);
}, poolSize: 64);


builder.Services.AddOptions<Destiny2Options>()
    .Configure<IConfiguration>((settings, configuration) =>
    {
        configuration.GetSection("Destiny2Api").Bind(settings);
    });

builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration["RedisConnectionString"];
});

builder.Services.AddHttpClient<IDestiny2ApiClient, Destiny2ApiClient>();
builder.Services.AddScoped<IDestiny2Service, Destiny2Service>();
builder.Services.AddScoped<IQueryService, QueryService>();

builder.Services.AddSingleton(sp =>
{
    var json = new JsonSerializerOptions(JsonSerializerDefaults.Web);
    json.Converters.Add(new Int64AsStringJsonConverter());
    return json;
});

builder.Build().Run();
