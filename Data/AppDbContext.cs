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
        public DbSet<OpType> OpTypes { get; set; }
        public DbSet<ActivityType> ActivityTypes { get; set; }
        public DbSet<Activity> Activities { get; set; }
        public DbSet<ActivityReport> ActivityReports { get; set; }
        public DbSet<ActivityHashMapping> ActivityHashMappings { get; set; }

        public async Task<T> ExecuteInTransactionAsync<T>(Func<Task<T>> operation, CancellationToken ct, int maxRetries = 3)
        {
            var retryCount = 0;
            while (retryCount < maxRetries)
            {
                using var transaction = await Database.BeginTransactionAsync(ct);
                try
                {
                    var result = await operation();
                    await transaction.CommitAsync();
                    return result;
                }
                catch (DbUpdateConcurrencyException)
                {
                    retryCount++;
                    if (retryCount >= maxRetries)
                        throw;
                }
                catch (Exception)
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            }
            throw new InvalidOperationException("Operation failed after maximum retries.");
        }

        public async Task ExecuteInTransactionAsync(Func<Task> operation, CancellationToken ct, int maxRetries = 3)
        {
            await ExecuteInTransactionAsync(async () =>
            {
                await operation();
                return true;
            }, ct, maxRetries);
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Player>(entity =>
            {
                entity.Property(p => p.Id).ValueGeneratedNever();

                entity.HasMany(p => p.ActivityReports)
                      .WithOne(ar => ar.Player)
                      .HasForeignKey(ar => ar.PlayerId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(p => p.LastActivityReport)
                      .WithOne()
                      .HasForeignKey<Player>(p => p.LastPlayedActivityId)
                      .OnDelete(DeleteBehavior.SetNull);
            });

            modelBuilder.Entity<Activity>()
                .Property(a => a.Id)
                .ValueGeneratedNever();

            modelBuilder.Entity<ActivityType>()
                .Property(at => at.Id)
                .ValueGeneratedNever();

            modelBuilder.Entity<ActivityReport>(entity =>
            {
                entity.HasIndex(ar => ar.InstanceId);

                entity.HasIndex(ar => new { ar.ActivityId, ar.PlayerId })
                      .HasDatabaseName("IX_ActivityReport_Activity_Player");

                entity.HasIndex(ar => new { ar.ActivityId, ar.PlayerId, ar.Duration })
                      .HasFilter("\"Completed\" = TRUE")
                      .HasDatabaseName("IX_ActivityReport_Completed_Fastest");

                entity.HasIndex(ar => new { ar.ActivityId, ar.PlayerId, ar.Completed })
                      .IncludeProperties(ar => new { ar.Duration })
                      .HasDatabaseName("IX_ActivityReport_Activity_Player_Completed_InclDuration");
            });

            modelBuilder.Entity<ActivityHashMapping>()
                .HasKey(m => m.SourceHash);

            modelBuilder.Entity<ActivityHashMapping>()
                .HasOne(m => m.CanonicalActivity)
                .WithMany()
                .HasForeignKey(m => m.CanonicalActivityId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
