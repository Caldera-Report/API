using API.Clients;
using API.Clients.Abstract;
using Crawler.Registries;
using Crawler.Services;
using Domain.Configuration;
using Domain.Data;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
using Sentry;
using Sentry.Extensions.Logging;

await Host.CreateDefaultBuilder(args)
    .ConfigureLogging((ctx, logging) =>
    {
        logging.AddConsole();
        logging.AddSentry(options =>
        {
            ctx.Configuration.GetSection("Sentry").Bind(options);

            options.Dsn ??= Environment.GetEnvironmentVariable("SENTRY_DSN");
            options.Environment = ctx.HostingEnvironment.EnvironmentName;
            options.Debug = ctx.HostingEnvironment.IsDevelopment();
        });

        if (ctx.HostingEnvironment.IsDevelopment())
        {
            logging.AddFilter("Microsoft.Extensions.Logging.Console", LogLevel.Debug);
        }
        else
        {
            logging.AddFilter("Microsoft.Extensions.Logging.Console", LogLevel.Information);
        }
    })
    .ConfigureServices((ctx, services) =>
    {
        services.AddSingleton<IConnectionMultiplexer>(
            ConnectionMultiplexer.Connect(ctx.Configuration.GetConnectionString("RedisConnectionString") ?? throw new InvalidOperationException("Redis connection string is not configured"))
        );
        services.AddHttpClient();
        services.AddDbContextFactory<AppDbContext>(options =>
        {
            options.UseNpgsql(ctx.Configuration.GetConnectionString("PostgreSqlConnectionString"), npgsqlOptions =>
            {
                npgsqlOptions.EnableRetryOnFailure(
                    maxRetryCount: 3,
                    maxRetryDelay: TimeSpan.FromSeconds(30),
                    errorCodesToAdd: null);
            });

            options.EnableServiceProviderCaching();
            options.EnableSensitiveDataLogging(ctx.HostingEnvironment.IsDevelopment());
        });

        services.AddOptions<Destiny2Options>()
            .Configure<IConfiguration>((settings, configuration) =>
            {
                configuration.GetSection("Destiny2Api").Bind(settings);
            });

        services.AddSingleton<RateLimiterRegistry>();

        services.AddHttpClient<IDestiny2ApiClient, Destiny2ApiClient>();

        services.AddHostedService<PipelineOrchestrator>();
    })
    .RunConsoleAsync();
