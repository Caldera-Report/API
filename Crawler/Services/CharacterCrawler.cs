using API.Clients.Abstract;
using Domain.Data;
using Domain.DB;
using Domain.DestinyApi;
using Domain.DTO;
using Microsoft.EntityFrameworkCore;
using Npgsql.EntityFrameworkCore.PostgreSQL.Query.ExpressionTranslators.Internal;
using StackExchange.Redis;
using System.Threading.Channels;

namespace Crawler.Services
{
    public class CharacterCrawler
    {
        private readonly IDatabase _cache;
        private readonly IDbContextFactory<AppDbContext> _contextFactory;
        private readonly IDestiny2ApiClient _client;
        private readonly ChannelReader<CharacterWorkItem> _input;
        private readonly ChannelWriter<long> _output;
        private readonly ILogger<CharacterCrawler> _logger;

        private const int MaxConcurrentTasks = 20;
        private static readonly DateTime ActivityCutoffUtc = new DateTime(2025, 7, 15);
        private readonly Dictionary<long, long> _activityHashMap;

        public CharacterCrawler(
            IConnectionMultiplexer redis,
            IDestiny2ApiClient client,
            ChannelReader<CharacterWorkItem> input,
            ChannelWriter<long> output,
            ILogger<CharacterCrawler> logger,
            IDbContextFactory<AppDbContext> contextFactory)
        {
            _cache = redis.GetDatabase();
            _client = client;
            _input = input;
            _output = output;
            _logger = logger;
            _contextFactory = contextFactory;

            var entries = _cache.HashGetAll("activityHashMappings");
            _activityHashMap = entries.ToDictionary(
                x => long.TryParse(x.Name, out var nameHash) ? nameHash : 0,
                x => long.TryParse(x.Value, out var valueHash) ? valueHash : 0
            );
        }

        public async Task RunAsync(CancellationToken ct)
        {
            var activeTasks = new List<Task>();
            _logger.LogInformation("Character crawler started.");
            try
            {
                await foreach (var item in _input.ReadAllAsync(ct))
                {
                    while (activeTasks.Count >= MaxConcurrentTasks)
                    {
                        var completedTask = await Task.WhenAny(activeTasks);
                        activeTasks.Remove(completedTask);
                    }

                    activeTasks.Add(ProcessItemAsync(item, ct));
                }

                await Task.WhenAll(activeTasks);
                _logger.LogInformation("Character crawler completed.");
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Character crawler cancellation requested.");
            }
            finally
            {
                _output.Complete();
            }
        }

        private async Task ProcessItemAsync(CharacterWorkItem item, CancellationToken ct)
        {
            try
            {
                await using var context = await _contextFactory.CreateDbContextAsync(ct);
                var player = await context.Players.FindAsync(item.PlayerId, ct) ?? throw new InvalidDataException($"Player with Id {item.PlayerId} cannot be found");
                var lastPlayedDate = await context.ActivityReports
                    .AsNoTracking()
                    .Where(r => r.Players.Any(p => p.PlayerId == item.PlayerId))
                    .OrderByDescending(r => r.Date)
                    .Select(r => (DateTime?)r.Date)
                    .FirstOrDefaultAsync(ct);

                var reportIds = await GetCharacterActivityReports(player, lastPlayedDate ?? ActivityCutoffUtc, item.CharacterId, ct);
                foreach (var reportId in reportIds)
                {
                    await _output.WriteAsync(reportId, ct);
                }
                player.NeedsFullCheck = false;
                await context.SaveChangesAsync(ct);
                _logger.LogInformation(
                    "Queued {ReportCount} activity reports for player {PlayerId} character {CharacterId}.",
                    reportIds.Length,
                    item.PlayerId,
                    item.CharacterId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing CharacterWorkItem for player: {PlayerId}, character: {CharacterId}", item.PlayerId, item.CharacterId);
            }
        }

        public async Task<long[]> GetCharacterActivityReports(Player player, DateTime lastPlayedActivityDate, string characterId, CancellationToken ct)
        {
            lastPlayedActivityDate = player.NeedsFullCheck ? ActivityCutoffUtc : lastPlayedActivityDate;

            var page = 0;
            var reportIds = new List<long>();
            var hasReachedLastUpdate = false;
            var activityCount = 250;

            try
            {
                while (!hasReachedLastUpdate)
                {
                    var response = await _client.GetHistoricalStatsForCharacter(player.Id, player.MembershipType, characterId, page, activityCount, ct);
                    if (response.Response?.activities == null || !response.Response.activities.Any())
                        break;
                    page++;
                    foreach (var activity in response.Response.activities)
                    {
                        hasReachedLastUpdate = activity.period <= lastPlayedActivityDate;
                        if (activity.period < ActivityCutoffUtc || hasReachedLastUpdate)
                            break;
                        var rawHash = activity.activityDetails.referenceId;
                        if (!_activityHashMap.TryGetValue(rawHash, out var canonicalId))
                            continue;
                        if (!long.TryParse(activity.activityDetails.instanceId, out var instanceId))
                            continue;

                        reportIds.Add(instanceId);
                    }
                    if (response.Response.activities.Last().period < ActivityCutoffUtc)
                        break;
                }
                return reportIds.ToArray();
            }
            catch (DestinyApiException ex) when (ex.ErrorCode == 1665)
            {
                _logger.LogWarning(ex, "Historical stats throttled for player {PlayerId} character {CharacterId}.", player.Id, characterId);
                return Array.Empty<long>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching activity reports for player: {PlayerId}, character: {CharacterId}", player.Id, characterId);
                throw;
            }
        }
    }
}
