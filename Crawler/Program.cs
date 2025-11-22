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
    var cfg = b.Configuration;
    var baseEndpoint = cfg["OpenTelemetry:Endpoint"]?.TrimEnd('/');

    if (string.IsNullOrWhiteSpace(baseEndpoint))
        throw new InvalidOperationException("OpenTelemetry:Endpoint must be set.");

    // Build correct per-signal endpoints
    var tracesEndpoint = new Uri($"{baseEndpoint}/v1/traces");
    var metricsEndpoint = new Uri($"{baseEndpoint}/v1/metrics");
    var logsEndpoint = new Uri($"{baseEndpoint}/v1/logs");

    var samplingRatio = Math.Clamp(cfg.GetValue<double?>("OpenTelemetry:TraceSamplingRatio") ?? 1.0, 0.0001, 1.0);

    Action<ResourceBuilder> configureResource = resource =>
    {
        var assembly = typeof(PipelineOrchestrator).Assembly.GetName();
        var serviceName = b.Environment.ApplicationName ?? assembly.Name ?? "Crawler";
        var serviceVersion = assembly.Version?.ToString() ?? "unknown";

        resource.AddService(serviceName, serviceVersion, Environment.MachineName)
                .AddAttributes(new[]
                {
                    new KeyValuePair<string, object>("deployment.environment", b.Environment.EnvironmentName),
                });
    };

    // Reusable batch config
    Action<BatchExportProcessorOptions<Activity>> configureBatch = batch =>
    {
        batch.MaxQueueSize = 2048;
        batch.ScheduledDelayMilliseconds = 5000;
        batch.ExporterTimeoutMilliseconds = 30000;
        batch.MaxExportBatchSize = 512;
    };

    string headers = cfg["OpenTelemetry:Headers"];

    b.Services.AddOpenTelemetry()
        .ConfigureResource(configureResource)
        .WithMetrics(metrics =>
        {
            metrics.AddRuntimeInstrumentation();
            metrics.AddHttpClientInstrumentation();
            metrics.AddAspNetCoreInstrumentation();
            metrics.AddNpgsqlInstrumentation();

            metrics.AddOtlpExporter(opts =>
            {
                opts.Endpoint = metricsEndpoint;
                opts.Protocol = OtlpExportProtocol.HttpProtobuf;
                if (!string.IsNullOrWhiteSpace(headers))
                    opts.Headers = headers;
            });
        })
        .WithTracing(tracing =>
        {
            tracing.AddHttpClientInstrumentation();
            tracing.AddAspNetCoreInstrumentation();
            tracing.AddNpgsql();
            tracing.AddSource(CrawlerTelemetry.ActivitySourceName);
            tracing.SetSampler(new TraceIdRatioBasedSampler(samplingRatio));

            tracing.AddOtlpExporter(opts =>
            {
                opts.Endpoint = tracesEndpoint;
                opts.Protocol = OtlpExportProtocol.HttpProtobuf;
                configureBatch(opts.BatchExportProcessorOptions);
                if (!string.IsNullOrWhiteSpace(headers))
                    opts.Headers = headers;
            });
        });

    b.Logging.AddOpenTelemetry(options =>
    {
        options.IncludeScopes = true;
        options.ParseStateValues = true;
        options.IncludeFormattedMessage = true;

        var resourceBuilder = ResourceBuilder.CreateDefault();
        configureResource(resourceBuilder);
        options.SetResourceBuilder(resourceBuilder);

        options.AddOtlpExporter(opts =>
        {
            opts.Endpoint = logsEndpoint;
            opts.Protocol = OtlpExportProtocol.HttpProtobuf;
            if (!string.IsNullOrWhiteSpace(headers))
                opts.Headers = headers;
        });
    });

    // Logging filters
    b.Logging.AddFilter<OpenTelemetryLoggerProvider>(filter =>
    {
        return b.Environment.IsDevelopment()
            ? filter >= LogLevel.Information
            : filter >= LogLevel.Warning;
    });

    b.Logging.AddFilter("Microsoft.Extensions.Logging.Console",
        b.Environment.IsDevelopment() ? LogLevel.Debug : LogLevel.Information);
}

