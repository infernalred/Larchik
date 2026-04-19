using Larchik.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Larchik.Persistence.Configuration;

public class PositionSnapshotModelConfiguration : IEntityTypeConfiguration<PositionSnapshot>
{
    public void Configure(EntityTypeBuilder<PositionSnapshot> builder)
    {
        builder.HasQuantityPrecision(x => x.Quantity);
        builder.HasMoneyPrecision(x => x.CostBase);
        builder.HasMoneyPrecision(x => x.MarketValueBase);
        builder.HasMoneyPrecision(x => x.UnrealizedBase);
        builder.HasMoneyPrecision(x => x.RealizedBase);

        builder.HasIndex(x => new { x.PortfolioId, x.InstrumentId, x.Date }).IsUnique();
    }
}
