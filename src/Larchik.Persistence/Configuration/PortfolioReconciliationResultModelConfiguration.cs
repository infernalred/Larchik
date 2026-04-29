using Larchik.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Larchik.Persistence.Configuration;

public class PortfolioReconciliationResultModelConfiguration : IEntityTypeConfiguration<PortfolioReconciliationResult>
{
    public void Configure(EntityTypeBuilder<PortfolioReconciliationResult> builder)
    {
        builder.Property(x => x.Source).IsRequired().HasMaxLength(80);
        builder.Property(x => x.ReportingCurrencyId).IsRequired().HasMaxLength(3);
        builder.Property(x => x.Status).IsRequired().HasMaxLength(32);
        builder.Property(x => x.Severity).IsRequired().HasMaxLength(16);
        builder.Property(x => x.ReasonCode).IsRequired().HasMaxLength(64);
        builder.HasMoneyPrecision(x => x.ToleranceBase);
        builder.HasMoneyPrecision(x => x.ActualNavBase, precision: 20);
        builder.HasMoneyPrecision(x => x.ActualCashBase, precision: 20);
        builder.HasMoneyPrecision(x => x.ActualPositionsValueBase, precision: 20);
        builder.HasMoneyPrecision(x => x.TargetNavBase, precision: 20);
        builder.HasMoneyPrecision(x => x.TargetCashBase, precision: 20);
        builder.HasMoneyPrecision(x => x.TargetPositionsValueBase, precision: 20);
        builder.HasMoneyPrecision(x => x.NavDelta, precision: 20);
        builder.HasMoneyPrecision(x => x.CashDelta, precision: 20);
        builder.HasMoneyPrecision(x => x.PositionsDelta, precision: 20);
        builder.HasCreatedAt(x => x.CreatedAt, generatedOnAdd: true);

        builder.HasIndex(x => new { x.PortfolioId, x.StatementDate, x.Source, x.CreatedAt });
        builder.HasIndex(x => new { x.Status, x.StatementDate });
        builder.HasIndex(x => new { x.AlertRequired, x.StatementDate });

        builder.HasOne(x => x.Portfolio)
            .WithMany(x => x.ReconciliationResults)
            .HasForeignKey(x => x.PortfolioId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
