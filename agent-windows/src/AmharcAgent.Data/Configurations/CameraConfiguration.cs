using AmharcAgent.Core.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AmharcAgent.Data.Configurations;

public class CameraConfiguration : IEntityTypeConfiguration<Camera>
{
    public void Configure(EntityTypeBuilder<Camera> builder)
    {
        builder.HasKey(c => c.CameraId);
        builder.Property(c => c.Role).HasConversion<string>();
        // ConnectionState is runtime-only — not persisted
        builder.Ignore(c => c.ConnectionState);
        builder.Property(c => c.Name).IsRequired().HasMaxLength(100);
        builder.Property(c => c.IpAddress).HasMaxLength(45); // supports IPv6
        builder.Property(c => c.Username).HasMaxLength(64);
        builder.Property(c => c.Password).HasMaxLength(128);
    }
}
