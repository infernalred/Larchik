using Larchik.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Larchik.Persistence.Configuration;

public class LotModelConfiguration : IEntityTypeConfiguration<Lot>
{
    public void Configure(EntityTypeBuilder<Lot> builder)
    {
        builder.Property(x => x.CurrencyId).IsRequired().HasMaxLength(3);
        builder.Property(x => x.Quantity).HasPrecision(18, 6);
        builder.Property(x => x.RemainingQuantity).HasPrecision(18, 6);
        builder.Property(x => x.Cost).HasPrecision(18, 4);
        builder.Property(x => x.UpdatedAt);

        builder.HasIndex(x => new { x.PortfolioId, x.InstrumentId, x.Method });
    }
}
