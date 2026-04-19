using Larchik.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Larchik.Persistence.Configuration;

public class PortfolioSnapshotModelConfiguration : IEntityTypeConfiguration<PortfolioSnapshot>
{
    public void Configure(EntityTypeBuilder<PortfolioSnapshot> builder)
    {
        builder.HasMoneyPrecision(x => x.NavBase, precision: 20);
        builder.HasMoneyPrecision(x => x.PnlDayBase);
        builder.HasMoneyPrecision(x => x.PnlMonthBase);
        builder.HasMoneyPrecision(x => x.PnlYearBase);
        builder.HasMoneyPrecision(x => x.CashBase);

        builder.HasIndex(x => new { x.PortfolioId, x.Date }).IsUnique();
    }
}
