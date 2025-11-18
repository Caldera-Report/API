using System.Diagnostics;

namespace Crawler.Telemetry;

internal static class CrawlerTelemetry
{
    public const string ActivitySourceName = "Caldera.Crawler";

    public static readonly ActivitySource ActivitySource = new(ActivitySourceName);

    public static Activity? StartActivity(string name)
        => ActivitySource.StartActivity(name, ActivityKind.Internal);
}
