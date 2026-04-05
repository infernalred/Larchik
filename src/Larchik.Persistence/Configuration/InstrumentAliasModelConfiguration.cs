using Larchik.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Larchik.Persistence.Configuration;

public class InstrumentAliasModelConfiguration : IEntityTypeConfiguration<InstrumentAlias>
{
    public void Configure(EntityTypeBuilder<InstrumentAlias> builder)
    {
        builder.Property(x => x.AliasCode).IsRequired().HasMaxLength(32);
        builder.Property(x => x.NormalizedAliasCode).IsRequired().HasMaxLength(32);

        builder.HasIndex(x => x.NormalizedAliasCode).IsUnique();

        builder.HasOne(x => x.Instrument)
            .WithMany()
            .HasForeignKey(x => x.InstrumentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
