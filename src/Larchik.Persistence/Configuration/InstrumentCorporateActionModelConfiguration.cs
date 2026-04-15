using Larchik.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Larchik.Persistence.Configuration;

public class InstrumentCorporateActionModelConfiguration : IEntityTypeConfiguration<InstrumentCorporateAction>
{
    public void Configure(EntityTypeBuilder<InstrumentCorporateAction> builder)
    {
        builder.Property(x => x.Factor).HasPrecision(18, 6);
        builder.Property(x => x.Note).IsRequired().HasMaxLength(500);

        builder.HasIndex(x => new { x.InstrumentId, x.EffectiveDate });
        builder.HasIndex(x => new { x.InstrumentId, x.Type, x.EffectiveDate }).IsUnique();

        builder.HasOne(x => x.Instrument)
            .WithMany()
            .HasForeignKey(x => x.InstrumentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
