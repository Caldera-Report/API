using API.Clients.Abstract;
using Domain.Data;
using Domain.DestinyApi;
using Domain.DTO;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
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

        public PlayerCrawler(
            IConnectionMultiplexer redis,
            IDestiny2ApiClient client,
            ChannelWriter<CharacterWorkItem> output,
            ILogger<PlayerCrawler> logger,
            IDbContextFactory<AppDbContext> contextFactory)
        {
            _cache = redis.GetDatabase();
            _client = client;
            _output = output;
            _logger = logger;
            _contextFactory = contextFactory;
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
                try
                {
                    var playerValue = await _cache.ListLeftPopAsync("player-crawl-queue");
                    if (playerValue.IsNull)
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

                    var playerId = (long)playerValue;
                    if (playerId == 0)
                    {
                        _logger.LogInformation("Player crawl sentinel encountered; signaling completion.");
                        _output.Complete();
                        break;
                    }

                    await using var context = await _contextFactory.CreateDbContextAsync(ct);

                    var player = await context.Players.FirstOrDefaultAsync(p => p.Id == playerId, ct);
                    if (player is null)
                    {
                        _logger.LogWarning("Player {PlayerId} not found in database; skipping work item.", playerId);
                        continue;
                    }

                    var characters = await GetCharactersForPlayer(playerId, player.MembershipType, context, ct);

                    var queuedCharacters = 0;
                    foreach (var character in characters.Where(c => c.Value.dateLastPlayed > _lastUpdateStarted || player.NeedsFullCheck))
                    {
                        var workItem = new CharacterWorkItem(playerId, character.Key);
                        await _output.WriteAsync(workItem, ct);
                        queuedCharacters++;
                    }

                    if (queuedCharacters == 0)
                    {
                        _logger.LogDebug("No recent activity for player {PlayerId}; nothing queued.", playerId);
                    }
                    else
                    {
                        _logger.LogInformation("Queued {CharacterCount} characters for player {PlayerId}.", queuedCharacters, playerId);
                    }
                }
                catch (OperationCanceledException)
                {
                    _logger.LogInformation("Player crawler cancellation requested.");
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing player crawl queue.");
                }
            }
        }

        public async Task<Dictionary<string, DestinyCharacterComponent>> GetCharactersForPlayer(long membershipId, int membershipType, AppDbContext context, CancellationToken ct)
        {
            var characters = await _client.GetCharactersForPlayer(membershipId, membershipType, ct);

            if (characters.ErrorCode == 1665)
            {
                _logger.LogWarning("Player {PlayerId} has a private profile; skipping.", membershipId);
                return new Dictionary<string, DestinyCharacterComponent>();
            }

            await CheckPlayerNameAndEmblem(characters.Response, membershipId, context, ct);
            return characters.Response.characters.data;
        }

        public async Task CheckPlayerNameAndEmblem(DestinyProfileResponse profile, long id, AppDbContext context, CancellationToken ct)
        {
            var player = await context.Players.FirstOrDefaultAsync(p => p.Id == id, ct);
            if (player == null)
            {
                _logger.LogWarning("Skipping display name sync; player {PlayerId} not found.", id);
                return;
            }

            if (player.DisplayName != profile.profile.data.userInfo.bungieGlobalDisplayName ||
                player.DisplayNameCode != profile.profile.data.userInfo.bungieGlobalDisplayNameCode)
            {
                player.DisplayName = profile.profile.data.userInfo.bungieGlobalDisplayName;
                player.DisplayNameCode = profile.profile.data.userInfo.bungieGlobalDisplayNameCode;
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
