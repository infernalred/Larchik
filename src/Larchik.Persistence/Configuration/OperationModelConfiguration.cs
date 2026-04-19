using Larchik.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Larchik.Persistence.Configuration;

public class OperationModelConfiguration : IEntityTypeConfiguration<Operation>
{
    public void Configure(EntityTypeBuilder<Operation> builder)
    {
        builder.Property(x => x.BrokerOperationKey).HasMaxLength(48);
        builder.HasCurrencyCode(x => x.CurrencyId, required: true);
        builder.HasQuantityPrecision(x => x.Price);
        builder.HasQuantityPrecision(x => x.Quantity);
        builder.HasMoneyPrecision(x => x.Fee);
        builder.Property(x => x.Note).HasMaxLength(500);
        builder.HasCreatedAt(x => x.CreatedAt);
        builder.HasUpdatedAt(x => x.UpdatedAt);

        builder.HasIndex(x => new { x.PortfolioId, x.TradeDate, x.CreatedAt });
        builder.HasIndex(x => new { x.PortfolioId, x.InstrumentId, x.TradeDate });
        builder.HasIndex(x => new { x.InstrumentId, x.TradeDate });
        builder.HasIndex(x => new { x.PortfolioId, x.BrokerOperationKey })
            .IsUnique()
            .HasFilter("\"broker_operation_key\" IS NOT NULL");
    }
}
