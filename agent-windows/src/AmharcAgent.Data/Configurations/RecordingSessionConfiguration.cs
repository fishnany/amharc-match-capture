using AmharcAgent.Core.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AmharcAgent.Data.Configurations;

/// <summary>EF Core fluent configuration for the <see cref="RecordingSession"/> entity.</summary>
public class RecordingSessionConfiguration : IEntityTypeConfiguration<RecordingSession>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<RecordingSession> builder)
    {
        builder.HasKey(r => r.RecordingId);

        builder.Property(r => r.RecordingId)
            .IsRequired()
            .HasMaxLength(36);

        builder.Property(r => r.MatchId)
            .IsRequired()
            .HasMaxLength(36);

        builder.Property(r => r.CameraId)
            .IsRequired()
            .HasMaxLength(36);

        builder.Property(r => r.State)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(32);

        builder.Property(r => r.OutputDirectory)
            .IsRequired()
            .HasMaxLength(1024);

        builder.Property(r => r.RtspUrl)
            .IsRequired()
            .HasMaxLength(512);

        builder.Property(r => r.FinalFilePath)
            .HasMaxLength(1024);

        builder.Property(r => r.Checksum)
            .HasMaxLength(128);

        builder.Property(r => r.CreatedAt)
            .IsRequired();

        builder.Property(r => r.UpdatedAt)
            .IsRequired();

        builder.HasIndex(r => r.MatchId);
        builder.HasIndex(r => r.CameraId);
        builder.HasIndex(r => new { r.MatchId, r.CameraId });
    }
}
