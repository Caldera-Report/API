using API.Clients.Abstract;
using Crawler.Services;
using Domain.Data;
using Domain.DB;
using Domain.DestinyApi;
using Domain.DTO;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using StackExchange.Redis;
using System.Threading.Channels;

namespace Crawler.Tests;

public class PlayerCrawlerTests
{
    [Fact]
    public async Task RunAsync_CompletesOutputWhenSentinelEncountered()
    {
        var databaseMock = new Mock<IDatabase>();
        databaseMock.Setup(db => db.ListLength("last-update-started", CommandFlags.None)).Returns(0);
        databaseMock
            .SetupSequence(db => db.ListLeftPopAsync("player-crawl-queue", CommandFlags.None))
            .ReturnsAsync((RedisValue)0L);

        var multiplexerMock = new Mock<IConnectionMultiplexer>();
        multiplexerMock
            .Setup(m => m.GetDatabase(Moq.It.IsAny<int>(), Moq.It.IsAny<object>()))
            .Returns(databaseMock.Object);

        var clientMock = new Mock<IDestiny2ApiClient>();
        var contextFactoryMock = new Mock<IDbContextFactory<AppDbContext>>();
        var channel = Channel.CreateUnbounded<CharacterWorkItem>();

        var crawler = new PlayerCrawler(
            multiplexerMock.Object,
            clientMock.Object,
            channel.Writer,
            NullLogger<PlayerCrawler>.Instance,
            contextFactoryMock.Object);

        await crawler.RunAsync(CancellationToken.None);

        await channel.Reader.Completion.WaitAsync(TimeSpan.FromMilliseconds(100));
        Assert.False(channel.Reader.TryRead(out _));
        databaseMock.Verify(db => db.ListLeftPopAsync("player-crawl-queue", CommandFlags.None), Times.Once);
    }

    [Fact]
    public async Task RunAsync_EnqueuesCharacterWorkItemsForRecentCharacters()
    {
        const long playerId = 12345;
        var databaseMock = new Mock<IDatabase>();
        databaseMock.Setup(db => db.ListLength("last-update-started", CommandFlags.None)).Returns(0);
        databaseMock
            .SetupSequence(db => db.ListLeftPopAsync("player-crawl-queue", CommandFlags.None))
            .ReturnsAsync((RedisValue)playerId)
            .ReturnsAsync((RedisValue)0L);

        var multiplexerMock = new Mock<IConnectionMultiplexer>();
        multiplexerMock
            .Setup(m => m.GetDatabase(Moq.It.IsAny<int>(), Moq.It.IsAny<object>()))
            .Returns(databaseMock.Object);

        var dbName = Guid.NewGuid().ToString();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        await using (var seedContext = new AppDbContext(options))
        {
            seedContext.Players.Add(new Player
            {
                Id = playerId,
                MembershipType = 2,
                DisplayName = "Guardian",
                DisplayNameCode = 7777,
                NeedsFullCheck = false
            });
            await seedContext.SaveChangesAsync();
        }

        var contextFactory = new TestDbContextFactory(options);
        var channel = Channel.CreateUnbounded<CharacterWorkItem>();
        var clientMock = new Mock<IDestiny2ApiClient>();

        var apiResponse = new DestinyApiResponse<DestinyProfileResponse>
        {
            Response = new DestinyProfileResponse
            {
                profile = new DestinyProfile
                {
                    data = new ProfileData
                    {
                        userInfo = new UserInfoCard
                        {
                            bungieGlobalDisplayName = "Guardian",
                            bungieGlobalDisplayNameCode = 7777
                        }
                    }
                },
                characters = new DictionaryComponentResponseOfint64AndDestinyCharacterComponent
                {
                    data = new Dictionary<string, DestinyCharacterComponent>
                    {
                        ["recent-character"] = new DestinyCharacterComponent
                        {
                            emblemBackgroundPath = "/img/emblem1.png",
                            emblemPath = "/img/emblem1a.png",
                            dateLastPlayed = new DateTime(2025, 7, 16),
                            characterId = "recent-character"
                        },
                        ["old-character"] = new DestinyCharacterComponent
                        {
                            emblemBackgroundPath = "/img/emblem2.png",
                            emblemPath = "/img/emblem2a.png",
                            dateLastPlayed = new DateTime(2025, 7, 10),
                            characterId = "old-character"
                        }
                    }
                }
            },
            ErrorCode = 1,
            ErrorStatus = "Ok",
            Message = "Success",
            MessageData = new Dictionary<string, string>()
        };

        clientMock.Setup(client => client.GetCharactersForPlayer(playerId, 2, Moq.It.IsAny<CancellationToken>()))
            .ReturnsAsync(apiResponse);

        var crawler = new PlayerCrawler(
            multiplexerMock.Object,
            clientMock.Object,
            channel.Writer,
            NullLogger<PlayerCrawler>.Instance,
            contextFactory);

        await crawler.RunAsync(CancellationToken.None);

        Assert.True(channel.Reader.TryRead(out var workItem));
        Assert.Equal(playerId, workItem.PlayerId);
        Assert.Equal("recent-character", workItem.CharacterId);
        Assert.False(channel.Reader.TryRead(out _));
        await channel.Reader.Completion.WaitAsync(TimeSpan.FromMilliseconds(100));

        clientMock.Verify(client => client.GetCharactersForPlayer(playerId, 2, Moq.It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CheckPlayerNameAndEmblem_UpdatesPlayerWhenDisplayNameChanged()
    {
        var databaseMock = new Mock<IDatabase>();
        databaseMock.Setup(db => db.ListLength("last-update-started", CommandFlags.None)).Returns(0);

        var multiplexerMock = new Mock<IConnectionMultiplexer>();
        multiplexerMock
            .Setup(m => m.GetDatabase(Moq.It.IsAny<int>(), Moq.It.IsAny<object>()))
            .Returns(databaseMock.Object);

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using (var seedContext = new AppDbContext(options))
        {
            seedContext.Players.Add(new Player
            {
                Id = 42,
                MembershipType = 3,
                DisplayName = "OldName",
                DisplayNameCode = 1234,
                NeedsFullCheck = true
            });
            await seedContext.SaveChangesAsync();
        }

        await using var context = new AppDbContext(options);

        var clientMock = new Mock<IDestiny2ApiClient>();
        var channel = Channel.CreateUnbounded<CharacterWorkItem>();

        var crawler = new PlayerCrawler(
            multiplexerMock.Object,
            clientMock.Object,
            channel.Writer,
            NullLogger<PlayerCrawler>.Instance,
            new TestDbContextFactory(options));

        var profileResponse = new DestinyProfileResponse
        {
            profile = new DestinyProfile
            {
                data = new ProfileData
                {
                    userInfo = new UserInfoCard
                    {
                        bungieGlobalDisplayName = "NewName",
                        bungieGlobalDisplayNameCode = 5678
                    }
                }
            },
            characters = new DictionaryComponentResponseOfint64AndDestinyCharacterComponent
            {
                data = new Dictionary<string, DestinyCharacterComponent>()
            }
        };

        await crawler.CheckPlayerNameAndEmblem(profileResponse, 42, context, CancellationToken.None);
        await context.SaveChangesAsync();

        var updated = await context.Players.FirstOrDefaultAsync(p => p.Id == 42);
        Assert.NotNull(updated);
        Assert.Equal("NewName", updated!.DisplayName);
        Assert.Equal(5678, updated.DisplayNameCode);
    }
}
