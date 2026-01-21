using Larchik.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Larchik.Persistence.Configuration;

public class CashBalanceModelConfiguration : IEntityTypeConfiguration<CashBalance>
{
    public void Configure(EntityTypeBuilder<CashBalance> builder)
    {
        builder.Property(x => x.CurrencyId).IsRequired().HasMaxLength(3);
        builder.Property(x => x.Amount).HasPrecision(18, 2);
        builder.Property(x => x.UpdatedAt).ValueGeneratedOnAddOrUpdate();

        builder.HasIndex(x => new { x.PortfolioId, x.CurrencyId }).IsUnique();

        builder.HasOne(x => x.Portfolio)
            .WithMany(x => x.CashBalances)
            .HasForeignKey(x => x.PortfolioId);
    }
}
