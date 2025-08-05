using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using API.Data;
using Microsoft.Extensions.Configuration;
using Microsoft.Azure.Functions.Worker;
using API.Clients.Abstract;
using API.Clients;
using API.Services.Abstract;
using API.Services;

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

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
    options.EnableSensitiveDataLogging(false);
}, poolSize: 64);

builder.Services.AddHttpClient<IDestiny2ApiClient, Destiny2ApiClient>();
builder.Services.AddScoped<IDestiny2Service, Destiny2Service>();

// Application Insights isn't enabled by default. See https://aka.ms/AAt8mw4.
builder.Services
    .AddApplicationInsightsTelemetryWorkerService()
    .ConfigureFunctionsApplicationInsights();

builder.Build().Run();
