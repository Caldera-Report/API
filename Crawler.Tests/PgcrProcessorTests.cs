using Crawler.Services;
using Domain.Data;
using Domain.DB;
using Domain.DestinyApi;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using StackExchange.Redis;
using System.Threading.Channels;

namespace Crawler.Tests;

public class PgcrProcessorTests
{
    [Fact]
    public async Task ProcessPgcrAsync_PersistsReportAndQueuesNewPlayers()
    {
        var databaseMock = new Mock<IDatabase>();
        databaseMock
            .Setup(db => db.HashGetAll("activityHashMappings", Moq.It.IsAny<CommandFlags>()))
            .Returns(new[]
            {
                new HashEntry("1000", "2000")
            });
        databaseMock
            .Setup(db => db.ListRightPushAsync(
                "player-crawl-queue",
                Moq.It.IsAny<RedisValue[]>(),
                Moq.It.IsAny<When>(),
                Moq.It.IsAny<CommandFlags>()))
            .ReturnsAsync(1L);

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
            seedContext.Activities.Add(new Activity
            {
                Id = 2000,
                Name = "Test Activity",
                ImageURL = "test.png",
                Index = 1,
                OpTypeId = 1
            });
            seedContext.OpTypes.Add(new OpType
            {
                Id = 1,
                Name = "Default"
            });
            await seedContext.SaveChangesAsync();
        }

        var processor = new PgcrProcessor(
            Channel.CreateUnbounded<PostGameCarnageReportData>().Reader,
            multiplexerMock.Object,
            new TestDbContextFactory(options),
            NullLogger<PgcrProcessor>.Instance);

        var pgcr = new PostGameCarnageReportData
        {
            period = new DateTime(2025, 8, 1),
            activityDetails = new Activitydetails
            {
                instanceId = "5555",
                referenceId = 1000
            },
            entries = new[]
            {
                new Entry
                {
                    player = new PGCRPlayer
                    {
                        destinyUserInfo = new Destinyuserinfo
                        {
                            isPublic = true,
                            membershipId = "12345",
                            membershipType = 2,
                            displayName = "Guardian",
                            bungieGlobalDisplayName = "Guardian",
                            bungieGlobalDisplayNameCode = 4444
                        }
                    },
                    values = new Values
                    {
                        score = new Score1 { basic = new Basic9 { value = 250f } },
                        completed = new Completed { basic = new Basic2 { value = 1 } },
                        completionReason = new Completionreason { basic = new Basic11 { value = 1 } },
                        activityDurationSeconds = new Activitydurationseconds { basic = new Basic10 { value = 600 } }
                    }
                }
            }
        };

        await processor.ProcessPgcrAsync(pgcr, CancellationToken.None);

        await using (var verificationContext = new AppDbContext(options))
        {
            var activityReport = await verificationContext.ActivityReports.SingleAsync();
            Assert.Equal(5555L, activityReport.Id);
            Assert.Equal(2000L, activityReport.ActivityId);
            Assert.False(activityReport.NeedsFullCheck);

            var player = await verificationContext.Players.SingleAsync();
            Assert.Equal(12345L, player.Id);
            Assert.Equal(2, player.MembershipType);
            Assert.Equal("Guardian", player.DisplayName);
            Assert.Equal(4444, player.DisplayNameCode);

            var reportPlayer = await verificationContext.ActivityReportPlayers.SingleAsync();
            Assert.Equal(5555L, reportPlayer.ActivityReportId);
            Assert.Equal(12345L, reportPlayer.PlayerId);
            Assert.Equal(250, reportPlayer.Score);
            Assert.True(reportPlayer.Completed);
            Assert.Equal(TimeSpan.FromSeconds(600), reportPlayer.Duration);
        }

        databaseMock.Verify(db => db.ListRightPushAsync(
            "player-crawl-queue",
            Moq.It.Is<RedisValue[]>(values => values.Length == 1 && values[0] == (RedisValue)12345L),
            Moq.It.IsAny<When>(),
            Moq.It.IsAny<CommandFlags>()),
            Times.Once);
    }
}
