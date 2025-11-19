using API.Clients;
using API.Clients.Abstract;
using Crawler.Jobs;
using Crawler.Registries;
using Crawler.Services;
using Crawler.Services.Abstract;
using Crawler.Telemetry;
using Domain.Configuration;
using Domain.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Npgsql;
using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Quartz;
using StackExchange.Redis;
using System.Diagnostics;

var builder = Host.CreateApplicationBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();

ConfigureOpenTelemetry(builder);

builder.Services.AddSingleton<IConnectionMultiplexer>(
    ConnectionMultiplexer.Connect(builder.Configuration.GetConnectionString("RedisConnectionString") ?? throw new InvalidOperationException("Redis connection string is not configured"))
);
builder.Services.AddHttpClient();
builder.Services.AddDbContextFactory<AppDbContext>(options =>
{
    options.UseNpgsql(builder.Configuration.GetConnectionString("PostgreSqlConnectionString"), npgsqlOptions =>
    {
        npgsqlOptions.EnableRetryOnFailure(
            maxRetryCount: 3,
            maxRetryDelay: TimeSpan.FromSeconds(30),
            errorCodesToAdd: null);
    });
    options.EnableSensitiveDataLogging(true);
});
// Provide scoped AppDbContext via factory for components that expect the context directly.
builder.Services.AddScoped(sp => sp.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContext());

builder.Services.AddOptions<Destiny2Options>()
    .Configure<IConfiguration>((settings, configuration) =>
    {
        configuration.GetSection("Destiny2Api").Bind(settings);
    });

builder.Services.AddSingleton<RateLimiterRegistry>();
builder.Services.AddHttpClient<IDestiny2ApiClient, Destiny2ApiClient>();
builder.Services.AddSingleton<PipelineOrchestrator>();
builder.Services.AddSingleton<ILeaderboardService, LeaderboardService>();
builder.Services.AddHostedService<RedisCrawlerTriggerListener>();

// ---- Quartz ----
builder.Services.AddQuartz(quartz =>
{
    var jobKey = new JobKey("PipelineOrchestratorJob");
    quartz.AddJob<PipelineOrchestratorJob>(opts => opts.WithIdentity(jobKey));

    quartz.AddTrigger(opts => opts
        .ForJob(jobKey)
        .WithIdentity("PipelineOrchestratorTrigger")
        .WithSchedule(CronScheduleBuilder.DailyAtHourAndMinute(0, 0))); // midnight UTC
});
builder.Services.AddQuartzHostedService(options => options.WaitForJobsToComplete = true);

var host = builder.Build();

await host.RunAsync();

// ---- Helpers ----
void ConfigureOpenTelemetry(HostApplicationBuilder b)
{
    var samplingRatio = Math.Clamp(b.Configuration.GetValue<double?>("OpenTelemetry:TraceSamplingRatio") ?? 1.0, 0.0001, 1.0);

    Action<ResourceBuilder> configureResource = resource =>
    {
        var assembly = typeof(PipelineOrchestrator).Assembly.GetName();
        var serviceName = b.Environment.ApplicationName ?? assembly.Name ?? "Crawler";
        var serviceVersion = assembly.Version?.ToString() ?? "unknown";

        resource.AddService(serviceName: serviceName, serviceVersion: serviceVersion, serviceInstanceId: Environment.MachineName)
                .AddAttributes(new[]
                {
                    new KeyValuePair<string, object>("deployment.environment", b.Environment.EnvironmentName)
                });
    };

    Action<OtlpExporterOptions> configureExporter = options =>
    {
        var endpoint = b.Configuration["OpenTelemetry:Endpoint"];
        if (!string.IsNullOrWhiteSpace(endpoint) && Uri.TryCreate(endpoint, UriKind.Absolute, out var otlpEndpoint))
        {
            options.Endpoint = otlpEndpoint;
        }

        options.BatchExportProcessorOptions = new BatchExportProcessorOptions<Activity>
        {
            MaxQueueSize = 2048,
            ScheduledDelayMilliseconds = 5000,
            ExporterTimeoutMilliseconds = 30000,
            MaxExportBatchSize = 512,
        };

        options.Protocol = OtlpExportProtocol.Grpc;

        var headers = b.Configuration["OpenTelemetry:Headers"];
        if (!string.IsNullOrWhiteSpace(headers))
        {
            options.Headers = headers;
        }
    };

    b.Services.AddOpenTelemetry()
        .ConfigureResource(configureResource)
        .WithMetrics(metrics =>
        {
            metrics.AddRuntimeInstrumentation();
            metrics.AddHttpClientInstrumentation();
            metrics.AddAspNetCoreInstrumentation();
            metrics.AddNpgsqlInstrumentation();
            metrics.AddOtlpExporter(configureExporter);
        })
        .WithTracing(tracing =>
        {
            tracing.AddHttpClientInstrumentation();
            tracing.AddAspNetCoreInstrumentation();
            tracing.AddNpgsql();
            tracing.AddSource(CrawlerTelemetry.ActivitySourceName);
            tracing.SetSampler(new TraceIdRatioBasedSampler(samplingRatio));
            tracing.AddOtlpExporter(configureExporter);
        });

    b.Logging.AddOpenTelemetry(options =>
    {
        options.IncludeScopes = true;
        options.ParseStateValues = true;
        options.IncludeFormattedMessage = true;

        var resourceBuilder = ResourceBuilder.CreateDefault();
        configureResource(resourceBuilder);
        options.SetResourceBuilder(resourceBuilder);

        options.AddOtlpExporter(configureExporter);
    });

    b.Logging.AddFilter<OpenTelemetryLoggerProvider>(filter =>
    {
        if (b.Environment.IsDevelopment())
        {
            return filter >= LogLevel.Information;
        }
        else
        {
            return filter >= LogLevel.Warning;
        }
    });

    if (b.Environment.IsDevelopment())
    {
        b.Logging.AddFilter("Microsoft.Extensions.Logging.Console", LogLevel.Debug);
    }
    else
    {
        b.Logging.AddFilter("Microsoft.Extensions.Logging.Console", LogLevel.Information);
    }
}
