using AmharcAgent.Core.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AmharcAgent.Data.Configurations;

/// <summary>EF Core fluent configuration for the <see cref="StreamingDestination"/> entity.</summary>
public class StreamingDestinationConfiguration : IEntityTypeConfiguration<StreamingDestination>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<StreamingDestination> builder)
    {
        builder.HasKey(d => d.DestinationId);

        builder.Property(d => d.DestinationId)
            .IsRequired()
            .HasMaxLength(36);

        builder.Property(d => d.Platform)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(32);

        builder.Property(d => d.Name)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(d => d.ServerUrl)
            .IsRequired()
            .HasMaxLength(512);

        builder.Property(d => d.StreamKey)
            .IsRequired()
            .HasMaxLength(512);

        builder.Property(d => d.Resolution)
            .HasMaxLength(32);

        builder.Property(d => d.CreatedAt)
            .IsRequired();

        builder.Property(d => d.UpdatedAt)
            .IsRequired();

        builder.HasIndex(d => d.Platform);
        builder.HasIndex(d => d.IsActive);
    }
}
