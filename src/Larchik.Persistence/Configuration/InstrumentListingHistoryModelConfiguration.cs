using Larchik.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Larchik.Persistence.Configuration;

public class InstrumentListingHistoryModelConfiguration : IEntityTypeConfiguration<InstrumentListingHistory>
{
    public void Configure(EntityTypeBuilder<InstrumentListingHistory> builder)
    {
        builder.Property(x => x.Ticker).IsRequired().HasMaxLength(16);
        builder.Property(x => x.Figi).HasMaxLength(32);
        builder.Property(x => x.CurrencyId).IsRequired().HasMaxLength(3);
        builder.Property(x => x.Exchange).HasMaxLength(50);
        builder.Property(x => x.CreatedAt).ValueGeneratedOnAdd();
        builder.Property(x => x.UpdatedAt);

        builder.HasIndex(x => new { x.InstrumentId, x.EffectiveFrom }).IsUnique();
        builder.HasIndex(x => new { x.InstrumentId, x.EffectiveTo });

        builder.HasOne(x => x.Instrument)
            .WithMany(x => x.ListingHistory)
            .HasForeignKey(x => x.InstrumentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Currency)
            .WithMany()
            .HasForeignKey(x => x.CurrencyId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
