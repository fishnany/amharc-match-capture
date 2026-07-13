using AmharcAgent.Core.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AmharcAgent.Data.Configurations;

public class MatchConfiguration : IEntityTypeConfiguration<Match>
{
    public void Configure(EntityTypeBuilder<Match> builder)
    {
        builder.HasKey(m => m.MatchId);
        builder.Property(m => m.Sport).HasConversion<string>();
        builder.Property(m => m.Status).HasConversion<string>();
        builder.Property(m => m.PeriodStructure).HasConversion<string>();
        builder.Property(m => m.HomeTeam).IsRequired().HasMaxLength(100);
        builder.Property(m => m.AwayTeam).IsRequired().HasMaxLength(100);
        builder.Property(m => m.Competition).HasMaxLength(200);
        builder.Property(m => m.Season).HasMaxLength(20);
        builder.Ignore(m => m.HomeTotal);
        builder.Ignore(m => m.AwayTotal);
        builder.HasIndex(m => m.Status);
        builder.HasIndex(m => m.Date);
    }
}
