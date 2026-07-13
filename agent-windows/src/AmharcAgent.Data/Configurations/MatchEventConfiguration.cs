using AmharcAgent.Core.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AmharcAgent.Data.Configurations;

public class MatchEventConfiguration : IEntityTypeConfiguration<MatchEvent>
{
    public void Configure(EntityTypeBuilder<MatchEvent> builder)
    {
        builder.HasKey(e => e.EventId);
        builder.Property(e => e.Source).HasConversion<string>();
        builder.Property(e => e.ReviewStatus).HasConversion<string>();
        builder.Property(e => e.Team).HasConversion<string>();
        builder.Property(e => e.EventType).IsRequired().HasMaxLength(50);
        builder.Property(e => e.MatchId).IsRequired();
        builder.HasIndex(e => e.MatchId);
        builder.HasIndex(e => new { e.MatchId, e.Period });
        builder.HasIndex(e => new { e.MatchId, e.SystemTimestamp });
    }
}
