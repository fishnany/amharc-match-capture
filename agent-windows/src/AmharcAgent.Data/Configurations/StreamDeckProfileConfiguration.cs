using AmharcAgent.Core.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System.Text.Json;

namespace AmharcAgent.Data.Configurations;

public class StreamDeckProfileConfiguration : IEntityTypeConfiguration<StreamDeckProfile>
{
    public void Configure(EntityTypeBuilder<StreamDeckProfile> builder)
    {
        builder.HasKey(p => p.ProfileId);
        builder.Property(p => p.Name).IsRequired().HasMaxLength(100);
        builder.Property(p => p.Sport).HasMaxLength(50);
        // Store buttons list as JSON column
        builder.Property(p => p.Buttons)
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<List<StreamDeckButton>>(v, (JsonSerializerOptions?)null) ?? new())
            .HasColumnType("TEXT");
    }
}
