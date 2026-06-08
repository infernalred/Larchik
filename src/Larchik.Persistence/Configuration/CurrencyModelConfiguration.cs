using Larchik.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Larchik.Persistence.Configuration;

public class CurrencyModelConfiguration : IEntityTypeConfiguration<Currency>
{
    public void Configure(EntityTypeBuilder<Currency> builder)
    {
        builder.Property(x => x.Id).IsRequired().HasMaxLength(3);
        builder.Property(x => x.Name).IsRequired().HasMaxLength(120);

        builder.HasData(
            new Currency { Id = "RUB", Name = "Российский рубль" },
            new Currency { Id = "USD", Name = "Доллар США" },
            new Currency { Id = "EUR", Name = "Евро" });
    }
}
