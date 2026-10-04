using AmharcAgent.Core.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AmharcAgent.Data.Configurations;

public sealed class W1OccurrenceCreationConfiguration : IEntityTypeConfiguration<W1OccurrenceCreation>
{
    public void Configure(EntityTypeBuilder<W1OccurrenceCreation> b)
    {
        b.ToTable("W1OccurrenceCreations");
        b.HasKey(x => new { x.Issuer, x.OperationKey });
        b.HasIndex(x => x.OccurrenceId).IsUnique();
        b.HasIndex(x => new { x.Issuer, x.LocalMatchId }).IsUnique();
        // Deliberately no cascading FK: issued identity/non-reuse evidence survives
        // removal of a local Match representation and rollback of application code.
        b.Property(x => x.Issuer).HasMaxLength(512);
        b.Property(x => x.OperationKey).HasMaxLength(512);
        b.Property(x => x.OccurrenceId).HasMaxLength(36);
        b.Property(x => x.RequestSha256).HasMaxLength(64);
    }
}