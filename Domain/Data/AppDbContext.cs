using Domain.DB;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Domain.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }
        public DbSet<Player> Players { get; set; }
        public DbSet<OpType> OpTypes { get; set; }
        public DbSet<Activity> Activities { get; set; }
        public DbSet<ActivityReport> ActivityReports { get; set; }
        public DbSet<ActivityReportPlayer> ActivityReportPlayers { get; set; }
        public DbSet<PlayerCrawlQueue> PlayerCrawlQueue { get; set; }
        public DbSet<PlayerLeaderboard> PlayerLeaderboards { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Player>()
                .Property(p => p.Id).ValueGeneratedNever()
                ;

            modelBuilder.Entity<Activity>()
                .Property(a => a.Id)
                .ValueGeneratedNever();

            modelBuilder.Entity<ActivityReport>(entity =>
            {
                entity.HasIndex(ar => ar.Id);
                entity.Property(ar => ar.Id).ValueGeneratedNever();
                entity.HasIndex(ar => new { ar.Id, ar.ActivityId, ar.Date });
                entity.HasIndex(ar => new { ar.Id, ar.ActivityId });
            });

            modelBuilder.Entity<ActivityReportPlayer>(entity =>
            {
                entity.HasKey(arp => new { arp.ActivityReportId, arp.PlayerId });
                entity.HasIndex(arp => new { arp.ActivityReportId, arp.PlayerId })
                    .HasFilter("\"Completed\" = TRUE");
                entity.HasIndex(arp => new {arp.ActivityId, arp.Completed, arp.Duration})
                    .HasFilter("\"Completed\" = TRUE");
                entity.HasIndex(arp => new { arp.ActivityReportId, arp.PlayerId, arp.Score });
                entity.HasIndex(arp => new { arp.ActivityReportId, arp.PlayerId, arp.Duration });
                entity.HasIndex(arp => new { arp.ActivityId });
            });

            modelBuilder.Entity<PlayerLeaderboard>(entity =>
            {
                entity.HasKey(pl => new { pl.PlayerId, pl.ActivityId, pl.LeaderboardType });
                var jsonOpts = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true };
                entity.Property(pl => pl.Data)
                      .HasColumnType("jsonb")
                      .HasConversion(
                          v => JsonSerializer.Serialize(v, jsonOpts),
                          v => JsonSerializer.Deserialize<LeaderboardStat>(v, jsonOpts)!);
                entity.HasIndex(pl => new { pl.ActivityId, pl.LeaderboardType, pl.Rank });

            });

        }
    }
}
