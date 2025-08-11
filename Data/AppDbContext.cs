using Classes.DB;
using Microsoft.EntityFrameworkCore;

namespace API.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }
        public DbSet<Player> Players { get; set; }
        public DbSet<Activity> Activities { get; set; }
        public DbSet<ActivityType> ActivityTypes { get; set; }
        public DbSet<ActivityReport> ActivityReports { get; set; }
        public DbSet<PlayerActivityRecord> PlayerActivityRecords { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Player>()
                .Property(p => p.Id)
                .ValueGeneratedNever();
            modelBuilder.Entity<Activity>()
                .Property(a => a.Id)
                .ValueGeneratedNever();
            modelBuilder.Entity<ActivityType>()
                .Property(at => at.Id)
                .ValueGeneratedNever();
            modelBuilder.Entity<PlayerActivityRecord>()
                .HasOne(par => par.FastestCompletionActivityReport)
                .WithMany()
                .HasForeignKey(par => par.FastestCompletionActivityReportInstanceId)
                .HasPrincipalKey(ar => ar.InstanceId);
        }
    }
}
