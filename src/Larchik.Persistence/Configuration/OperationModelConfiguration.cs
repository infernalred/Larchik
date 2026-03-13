using Larchik.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Larchik.Persistence.Configuration;

public class OperationModelConfiguration : IEntityTypeConfiguration<Operation>
{
    public void Configure(EntityTypeBuilder<Operation> builder)
    {
        builder.Property(x => x.BrokerOperationKey).HasMaxLength(48);
        builder.Property(x => x.CurrencyId).IsRequired().HasMaxLength(3);
        builder.Property(x => x.Price).HasPrecision(18, 6);
        builder.Property(x => x.Quantity).HasPrecision(18, 6);
        builder.Property(x => x.Fee).HasPrecision(18, 4);
        builder.Property(x => x.Note).HasMaxLength(500);
        builder.Property(x => x.CreatedAt);
        builder.Property(x => x.UpdatedAt);

        builder.HasIndex(x => new { x.PortfolioId, x.TradeDate });
        builder.HasIndex(x => new { x.InstrumentId, x.TradeDate });
        builder.HasIndex(x => new { x.PortfolioId, x.BrokerOperationKey })
            .IsUnique()
            .HasFilter("\"broker_operation_key\" IS NOT NULL");
    }
}
