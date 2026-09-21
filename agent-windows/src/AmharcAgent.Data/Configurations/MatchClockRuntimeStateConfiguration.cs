using AmharcAgent.Core.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AmharcAgent.Data.Configurations;

public class MatchClockRuntimeStateConfiguration
    : IEntityTypeConfiguration<MatchClockRuntimeState>
{
    public void Configure(
        EntityTypeBuilder<MatchClockRuntimeState> builder)
    {
        builder.HasKey(c => c.MatchId);

        builder.Property(c => c.MatchId)
            .IsRequired()
            .HasMaxLength(36);

        builder.Property(c => c.ClockMode)
            .IsRequired()
            .HasMaxLength(32);

        builder.Property(c => c.PersistedAt)
            .IsRequired();

        builder.Property(c => c.CreatedAt)
            .IsRequired();

        builder.Property(c => c.UpdatedAt)
            .IsRequired();
    }
}