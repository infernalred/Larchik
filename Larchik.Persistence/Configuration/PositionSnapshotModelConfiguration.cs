using Larchik.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Larchik.Persistence.Configuration;

public class PositionSnapshotModelConfiguration : IEntityTypeConfiguration<PositionSnapshot>
{
    public void Configure(EntityTypeBuilder<PositionSnapshot> builder)
    {
        builder.Property(x => x.Quantity).HasPrecision(18, 6);
        builder.Property(x => x.CostBase).HasPrecision(18, 4);
        builder.Property(x => x.MarketValueBase).HasPrecision(18, 4);
        builder.Property(x => x.UnrealizedBase).HasPrecision(18, 4);
        builder.Property(x => x.RealizedBase).HasPrecision(18, 4);

        builder.HasIndex(x => new { x.PortfolioId, x.InstrumentId, x.Date }).IsUnique();
    }
}
