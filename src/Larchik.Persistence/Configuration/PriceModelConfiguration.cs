using Larchik.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Larchik.Persistence.Configuration;

public class PriceModelConfiguration : IEntityTypeConfiguration<Price>
{
    public void Configure(EntityTypeBuilder<Price> builder)
    {
        builder.HasCurrencyCode(x => x.CurrencyId, required: true);
        builder.HasCurrencyCode(x => x.SourceCurrencyId);
        builder.Property(x => x.Provider).IsRequired().HasMaxLength(50);
        builder.HasMoneyPrecision(x => x.Value);
        builder.HasCreatedAt(x => x.CreatedAt, generatedOnAdd: true);
        builder.HasUpdatedAt(x => x.UpdatedAt);

        builder.HasIndex(x => new { x.InstrumentId, x.Date, x.Provider }).IsUnique();
    }
}
