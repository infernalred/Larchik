using Larchik.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Larchik.Persistence.Configuration;

public class PortfolioSnapshotModelConfiguration : IEntityTypeConfiguration<PortfolioSnapshot>
{
    public void Configure(EntityTypeBuilder<PortfolioSnapshot> builder)
    {
        builder.Property(x => x.NavBase).HasPrecision(20, 4);
        builder.Property(x => x.PnlDayBase).HasPrecision(18, 4);
        builder.Property(x => x.PnlMonthBase).HasPrecision(18, 4);
        builder.Property(x => x.PnlYearBase).HasPrecision(18, 4);
        builder.Property(x => x.CashBase).HasPrecision(18, 4);

        builder.HasIndex(x => new { x.PortfolioId, x.Date }).IsUnique();
    }
}
