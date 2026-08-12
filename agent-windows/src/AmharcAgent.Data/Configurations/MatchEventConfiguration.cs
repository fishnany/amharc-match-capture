using System.Text.Json;
using AmharcAgent.Core.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AmharcAgent.Data.Configurations;

public class MatchEventConfiguration : IEntityTypeConfiguration<MatchEvent>
{
    public void Configure(EntityTypeBuilder<MatchEvent> builder)
    {
        builder.HasKey(e => e.EventId);

        builder.Property(e => e.Source)
            .HasConversion<string>();

        builder.Property(e => e.ReviewStatus)
            .HasConversion<string>();

        builder.Property(e => e.Team)
            .HasConversion<string>();

        builder.Property(e => e.EventType)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(e => e.MatchId)
            .IsRequired();

        builder.Property(e => e.ScoreBeforeState)
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<AmharcAgent.Core.Models.ScoreSnapshot>(
                    v,
                    (JsonSerializerOptions?)null))
            .HasColumnType("TEXT");

        builder.Property(e => e.ScoreAfterState)
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<AmharcAgent.Core.Models.ScoreSnapshot>(
                    v,
                    (JsonSerializerOptions?)null))
            .HasColumnType("TEXT");

        builder.HasIndex(e => e.MatchId);
        builder.HasIndex(e => new { e.MatchId, e.Period });
        builder.HasIndex(e => new { e.MatchId, e.SystemTimestamp });
    }
}

