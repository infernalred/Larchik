using Larchik.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Larchik.Persistence.Configuration;

public class FxRateModelConfiguration : IEntityTypeConfiguration<FxRate>
{
    public void Configure(EntityTypeBuilder<FxRate> builder)
    {
        builder.Property(x => x.BaseCurrencyId).IsRequired().HasMaxLength(3);
        builder.Property(x => x.QuoteCurrencyId).IsRequired().HasMaxLength(3);
        builder.Property(x => x.Source).IsRequired().HasMaxLength(50);
        builder.HasQuantityPrecision(x => x.Rate);
        builder.HasCreatedAt(x => x.CreatedAt, generatedOnAdd: true);

        builder.HasIndex(x => new { x.Source, x.Date });
        builder.HasIndex(x => new { x.BaseCurrencyId, x.QuoteCurrencyId, x.Date }).IsUnique();
    }
}
