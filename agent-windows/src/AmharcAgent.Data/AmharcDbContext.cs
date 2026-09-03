using AmharcAgent.Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace AmharcAgent.Data;

public class AmharcDbContext(DbContextOptions<AmharcDbContext> options) : DbContext(options)
{
    public DbSet<Match> Matches => Set<Match>();
    public DbSet<Camera> Cameras => Set<Camera>();
    public DbSet<MatchEvent> MatchEvents => Set<MatchEvent>();
    public DbSet<MatchClockRuntimeState> MatchClockRuntimeStates => Set<MatchClockRuntimeState>();
    public DbSet<RecordingSession> RecordingSessions => Set<RecordingSession>();
    public DbSet<StreamingDestination> StreamingDestinations => Set<StreamingDestination>();
    public DbSet<StreamDeckProfile> StreamDeckProfiles => Set<StreamDeckProfile>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AmharcDbContext).Assembly);
    }

    public override Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.State == EntityState.Modified)
            {
                entry.Property("UpdatedAt").CurrentValue = DateTimeOffset.UtcNow;
            }
        }
        return base.SaveChangesAsync(ct);
    }
}
