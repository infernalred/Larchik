using Larchik.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Larchik.Persistence.Configuration;

public class CurrencyModelConfiguration : IEntityTypeConfiguration<Currency>
{
    public void Configure(EntityTypeBuilder<Currency> builder)
    {
        builder.Property(x => x.Id).HasMaxLength(3);

        var rub = new Currency
        {
            Id = "RUB"
        };

        var usd = new Currency
        {
            Id = "USD"
        };

        var eur = new Currency
        {
            Id = "EUR"
        };

        builder.HasData(rub, usd, eur);
    }
}