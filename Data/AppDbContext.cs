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
            modelBuilder.Entity<Player>()
                .Property(p => p.Id)
                .ValueGeneratedNever();
            modelBuilder.Entity<Activity>()
                .Property(a => a.Id)
                .ValueGeneratedNever();
            modelBuilder.Entity<ActivityType>()
                .Property(at => at.Id)
                .ValueGeneratedNever();
            modelBuilder.Entity<ActivityReport>()
                .HasIndex(ar => ar.InstanceId);
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
