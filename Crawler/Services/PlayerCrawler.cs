using API.Clients.Abstract;
using Crawler.Telemetry;
using Domain.Data;
using Domain.DestinyApi;
using Domain.DTO;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading.Channels;

namespace Crawler.Services
{
    public class PlayerCrawler
    {
        private readonly IDatabase _cache;
        private IDbContextFactory<AppDbContext> _contextFactory;
        private readonly IDestiny2ApiClient _client;
        private readonly ChannelWriter<CharacterWorkItem> _output;
        private readonly ILogger<PlayerCrawler> _logger;
        private readonly DateTime _lastUpdateStarted;
        private readonly ConcurrentDictionary<long, int> _playerCharacterWorkCount;

        private static readonly DateTime ActivityCutoffUtc = new DateTime(2025, 7, 15);

        public PlayerCrawler(
            IConnectionMultiplexer redis,
            IDestiny2ApiClient client,
            ChannelWriter<CharacterWorkItem> output,
            ILogger<PlayerCrawler> logger,
            IDbContextFactory<AppDbContext> contextFactory,
            ConcurrentDictionary<long, int> playerCharacterWorkCount)
        {
            _cache = redis.GetDatabase();
            _client = client;
            _output = output;
            _logger = logger;
            _contextFactory = contextFactory;
            _playerCharacterWorkCount = playerCharacterWorkCount;
            var lastUpdateStartedLength = _cache.ListLength("last-update-started");
            var lastUpdateValue = lastUpdateStartedLength > 1 ? _cache.ListLeftPop("last-update-started") : RedisValue.Null;
            if (lastUpdateValue.HasValue && DateTime.TryParse(lastUpdateValue.ToString(), out var parsedLastUpdate))
            {
                _lastUpdateStarted = parsedLastUpdate;
            }
            else
            {
                _lastUpdateStarted = new DateTime(2025, 7, 15);
                _logger.LogDebug("Falling back to default last-update timestamp {Fallback}.", _lastUpdateStarted);
            }
        }

        public async Task RunAsync(CancellationToken ct)
        {
            _logger.LogInformation("Player crawler started processing queue.");
            var emptyCount = 0;
            while (!ct.IsCancellationRequested)
            {
                Activity? playerActivity = null;
                try
                {
                    var context = _contextFactory.CreateDbContext();
                    var playerValue = await context.PlayerCrawlQueue.Where(pcq => pcq.Status == PlayerQueueStatus.Queued || 
                    (pcq.Status == PlayerQueueStatus.Error && pcq.Attempts < 3)).FirstOrDefaultAsync(ct);
                    if (playerValue == null)
                    {
                        await Task.Delay(1000, ct);
                        emptyCount++;
                        if (emptyCount >= 300) //waited 5 minutes
                        {
                            _logger.LogInformation("Player crawl queue empty; signaling completion.");
                            _output.Complete();
                            break;
                        }
                        continue;
                    }

                    emptyCount = 0;

                    playerActivity = CrawlerTelemetry.StartActivity("PlayerCrawler.ProcessPlayer");
                    playerActivity?.SetTag("crawler.player.id", playerValue.PlayerId);
                    playerActivity?.SetTag("crawler.player.queueStatus", playerValue.Status.ToString());

                    playerValue.Status = PlayerQueueStatus.Processing;
                    playerValue.Attempts += 1;
                    await context.SaveChangesAsync(ct);
                    playerActivity?.SetTag("crawler.player.attempt", playerValue.Attempts);

                    var player = await context.Players.FirstOrDefaultAsync(p => p.Id == playerValue.PlayerId, ct);
                    if (player is null)
                    {
                        playerActivity?.SetStatus(ActivityStatusCode.Error, "Player not found");
                        _logger.LogWarning("Player {PlayerId} not found in database; skipping work item.", playerValue.Id);
                        continue;
                    }

                    var characters = await GetCharactersForPlayer(playerValue.PlayerId, player.MembershipType, context, ct);
                    var lastPlayedActivityDate = await context.ActivityReports
                        .AsNoTracking()
                        .Where(r => r.Players.Any(p => p.PlayerId == playerValue.PlayerId) && !r.NeedsFullCheck)
                        .OrderByDescending(r => r.Date)
                        .Select(r => (DateTime?)r.Date)
                        .FirstOrDefaultAsync(ct);

                    var queuedCharacters = 0;
                    foreach (var character in characters.Where(c => c.Value.dateLastPlayed > _lastUpdateStarted || player.NeedsFullCheck))
                    {
                        _playerCharacterWorkCount.AddOrUpdate(playerValue.PlayerId, 1, (_, existing) => existing + 1);
                        var workItem = new CharacterWorkItem(playerValue.PlayerId, character.Key, lastPlayedActivityDate ?? ActivityCutoffUtc);
                        await _output.WriteAsync(workItem, ct);
                        queuedCharacters++;
                    }

                    if (queuedCharacters == 0)
                    {
                        player.NeedsFullCheck = false;
                        playerValue.Status = PlayerQueueStatus.Completed;
                        playerValue.ProcessedAt = DateTime.UtcNow;
                        _playerCharacterWorkCount.TryRemove(playerValue.PlayerId, out _);
                        await context.SaveChangesAsync(ct);
                        _logger.LogInformation("No new activities for player {PlayerId}; marked as completed without queueing characters.", playerValue.PlayerId);
                        playerActivity?.SetTag("crawler.player.queuedCharacters", 0);
                        continue;
                    }
                    else
                    {
                        _logger.LogInformation("Queued {CharacterCount} characters for player {PlayerId}.", queuedCharacters, playerValue.PlayerId);
                        playerActivity?.SetTag("crawler.player.queuedCharacters", queuedCharacters);
                    }
                }
                catch (OperationCanceledException)
                {
                    _logger.LogInformation("Player crawler cancellation requested.");
                    break;
                }
                catch (Exception ex)
                {
                    playerActivity?.AddException(ex);
                    playerActivity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                    _logger.LogError(ex, "Error processing player crawl queue.");
                }
                finally
                {
                    playerActivity?.Dispose();
                }
            }
        }

        public async Task<Dictionary<string, DestinyCharacterComponent>> GetCharactersForPlayer(long membershipId, int membershipType, AppDbContext context, CancellationToken ct)
        {
            using var activity = CrawlerTelemetry.StartActivity("PlayerCrawler.GetCharacters");
            activity?.SetTag("crawler.player.id", membershipId);
            activity?.SetTag("crawler.membership.type", membershipType);

            try
            {
                var characters = await _client.GetCharactersForPlayer(membershipId, membershipType, ct);

                if (characters.ErrorCode == 1665)
                {
                    activity?.SetStatus(ActivityStatusCode.Error, "Profile private");
                    _logger.LogWarning("Player {PlayerId} has a private profile; skipping.", membershipId);
                    return new Dictionary<string, DestinyCharacterComponent>();
                }

                await CheckPlayerNameAndEmblem(characters.Response, membershipId, context, ct);
                activity?.SetTag("crawler.player.characterCount", characters.Response.characters.data.Count);
                return characters.Response.characters.data;
            }
            catch (Exception ex)
            {
                activity?.AddException(ex);
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                throw;
            }
        }

        public async Task CheckPlayerNameAndEmblem(DestinyProfileResponse profile, long id, AppDbContext context, CancellationToken ct)
        {
            using var activity = CrawlerTelemetry.StartActivity("PlayerCrawler.SyncProfile");
            activity?.SetTag("crawler.player.id", id);
            var player = await context.Players.FirstOrDefaultAsync(p => p.Id == id, ct);
            if (player == null)
            {
                activity?.SetStatus(ActivityStatusCode.Error, "Player not found");
                _logger.LogWarning("Skipping display name sync; player {PlayerId} not found.", id);
                return;
            }

            if (player.DisplayName != profile.profile.data.userInfo.bungieGlobalDisplayName ||
                player.DisplayNameCode != profile.profile.data.userInfo.bungieGlobalDisplayNameCode)
            {
                player.DisplayName = profile.profile.data.userInfo.bungieGlobalDisplayName;
                player.DisplayNameCode = profile.profile.data.userInfo.bungieGlobalDisplayNameCode;
                player.FullDisplayName = player.DisplayName + "#" + player.DisplayNameCode;
                context.Players.Update(player);
                await context.SaveChangesAsync(ct);
                _logger.LogInformation("Updated display information for player {PlayerId}.", id);
            }

            var lastPlayedCharacter = profile.characters.data.Values
                .OrderByDescending(cid => profile.characters.data[cid.characterId].dateLastPlayed)
                .FirstOrDefault();

            if (lastPlayedCharacter != null)
            {
                if (player.LastPlayedCharacterEmblemPath != lastPlayedCharacter.emblemPath || player.LastPlayedCharacterBackgroundPath != lastPlayedCharacter.emblemBackgroundPath)
                {
                    player.LastPlayedCharacterEmblemPath = lastPlayedCharacter.emblemPath;
                    player.LastPlayedCharacterBackgroundPath = lastPlayedCharacter.emblemBackgroundPath;
                    context.Players.Update(player);
                    await context.SaveChangesAsync(ct);
                    _logger.LogInformation("Updated last played character emblem for player {PlayerId}.", id);
                }
            }
        }
    }
}
