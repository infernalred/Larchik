using Larchik.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Larchik.Persistence.Configuration;

public class CurrencyModelConfiguration : IEntityTypeConfiguration<Currency>
{
    public void Configure(EntityTypeBuilder<Currency> builder)
    {
        builder.HasCurrencyCode(x => x.Id, required: true);

        builder.HasData(
            new Currency { Id = "RUB" },
            new Currency { Id = "USD" },
            new Currency { Id = "EUR" });
    }
}
