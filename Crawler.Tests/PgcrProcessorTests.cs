using Crawler.Services;
using Domain.Data;
using Domain.DB;
using Domain.DTO;
using Domain.DestinyApi;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using StackExchange.Redis;
using System.Collections.Concurrent;
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
        var multiplexerMock = new Mock<IConnectionMultiplexer>();
        multiplexerMock
            .Setup(m => m.GetDatabase(Moq.It.IsAny<int>(), Moq.It.IsAny<object>()))
            .Returns(databaseMock.Object);

        var dbName = Guid.NewGuid().ToString();
        const long processedPlayerId = 9999;
        const long newPlayerId = 12345;
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        await using (var seedContext = new AppDbContext(options))
        {
            seedContext.Players.Add(new Player
            {
                Id = processedPlayerId,
                MembershipType = 1,
                DisplayName = "Existing",
                DisplayNameCode = 1000,
                FullDisplayName = "Existing#1000"
            });
            seedContext.PlayerCrawlQueue.Add(new PlayerCrawlQueue
            {
                PlayerId = processedPlayerId,
                Status = PlayerQueueStatus.Queued
            });
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

        var playerActivityCount = new ConcurrentDictionary<long, int>();
        playerActivityCount[processedPlayerId] = 1;

        var processor = new PgcrProcessor(
            Channel.CreateUnbounded<PgcrWorkItem>().Reader,
            multiplexerMock.Object,
            new TestDbContextFactory(options),
            NullLogger<PgcrProcessor>.Instance,
            playerActivityCount);

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
                            membershipId = newPlayerId.ToString(),
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

        await processor.ProcessPgcrAsync(new PgcrWorkItem(pgcr, processedPlayerId), CancellationToken.None);

        await using (var verificationContext = new AppDbContext(options))
        {
            var activityReport = await verificationContext.ActivityReports.SingleAsync();
            Assert.Equal(5555L, activityReport.Id);
            Assert.Equal(2000L, activityReport.ActivityId);
            Assert.False(activityReport.NeedsFullCheck);

            var player = await verificationContext.Players.SingleAsync(p => p.Id == newPlayerId);
            Assert.Equal(2, player.MembershipType);
            Assert.Equal("Guardian", player.DisplayName);
            Assert.Equal(4444, player.DisplayNameCode);

            var reportPlayer = await verificationContext.ActivityReportPlayers.SingleAsync();
            Assert.Equal(5555L, reportPlayer.ActivityReportId);
            Assert.Equal(newPlayerId, reportPlayer.PlayerId);
            Assert.Equal(250, reportPlayer.Score);
            Assert.True(reportPlayer.Completed);
            Assert.Equal(TimeSpan.FromSeconds(600), reportPlayer.Duration);

            var newQueueEntry = await verificationContext.PlayerCrawlQueue.SingleAsync(q => q.PlayerId == newPlayerId);
            Assert.Equal(PlayerQueueStatus.Queued, newQueueEntry.Status);

            var processedQueueEntry = await verificationContext.PlayerCrawlQueue.SingleAsync(q => q.PlayerId == processedPlayerId);
            Assert.Equal(PlayerQueueStatus.Completed, processedQueueEntry.Status);
        }
    }

    [Fact]
    public async Task ProcessPgcrAsync_ReplacesReportsMarkedForFullCheck()
    {
        var databaseMock = new Mock<IDatabase>();
        databaseMock
            .Setup(db => db.HashGetAll("activityHashMappings", Moq.It.IsAny<CommandFlags>()))
            .Returns(new[] { new HashEntry("1000", "2000") });

        var multiplexerMock = new Mock<IConnectionMultiplexer>();
        multiplexerMock
            .Setup(m => m.GetDatabase(Moq.It.IsAny<int>(), Moq.It.IsAny<object>()))
            .Returns(databaseMock.Object);

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        const long processedPlayerId = 999;

        await using (var seedContext = new AppDbContext(options))
        {
            seedContext.OpTypes.Add(new OpType { Id = 1, Name = "Default" });
            seedContext.Activities.Add(new Activity
            {
                Id = 2000,
                Name = "Test Activity",
                ImageURL = "test.png",
                Index = 1,
                OpTypeId = 1
            });
            seedContext.ActivityReports.Add(new ActivityReport
            {
                Id = 5555,
                ActivityId = 2000,
                Date = new DateTime(2025, 7, 1),
                NeedsFullCheck = true
            });
            seedContext.Players.Add(new Player
            {
                Id = processedPlayerId,
                MembershipType = 2,
                DisplayName = "Existing",
                DisplayNameCode = 1234,
                FullDisplayName = "Existing#1234"
            });
            seedContext.PlayerCrawlQueue.Add(new PlayerCrawlQueue
            {
                PlayerId = processedPlayerId,
                Status = PlayerQueueStatus.Queued
            });
            await seedContext.SaveChangesAsync();
        }

        var playerActivityCount = new ConcurrentDictionary<long, int>();
        playerActivityCount[processedPlayerId] = 1;

        var processor = new PgcrProcessor(
            Channel.CreateUnbounded<PgcrWorkItem>().Reader,
            multiplexerMock.Object,
            new TestDbContextFactory(options),
            NullLogger<PgcrProcessor>.Instance,
            playerActivityCount);

        var pgcr = new PostGameCarnageReportData
        {
            period = new DateTime(2025, 8, 1),
            activityDetails = new Activitydetails
            {
                instanceId = "5555",
                referenceId = 1000
            },
            entries = Array.Empty<Entry>()
        };

        await processor.ProcessPgcrAsync(new PgcrWorkItem(pgcr, processedPlayerId), CancellationToken.None);

        await using var verificationContext = new AppDbContext(options);
        var reports = await verificationContext.ActivityReports.ToListAsync();
        Assert.Single(reports);
        var report = reports[0];
        Assert.Equal(pgcr.period, report.Date);
        Assert.False(report.NeedsFullCheck);

        var queueEntry = await verificationContext.PlayerCrawlQueue.SingleAsync(q => q.PlayerId == processedPlayerId);
        Assert.Equal(PlayerQueueStatus.Completed, queueEntry.Status);
    }
}
