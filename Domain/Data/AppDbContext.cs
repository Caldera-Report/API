using Domain.DB;
using Microsoft.EntityFrameworkCore;

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

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Player>()
                .Property(p => p.Id).ValueGeneratedNever();

            modelBuilder.Entity<Activity>()
                .Property(a => a.Id)
                .ValueGeneratedNever();

            modelBuilder.Entity<ActivityReport>(entity =>
            {
                entity.HasIndex(ar => ar.Id);
                entity.Property(ar => ar.Id).ValueGeneratedNever();
            });

            modelBuilder.Entity<ActivityReportPlayer>()
                .HasKey(arp => new { arp.ActivityReportId, arp.PlayerId });
        }
    }
}
