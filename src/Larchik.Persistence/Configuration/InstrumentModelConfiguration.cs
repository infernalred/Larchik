using Larchik.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Larchik.Persistence.Configuration;

public class InstrumentModelConfiguration : IEntityTypeConfiguration<Instrument>
{
    public void Configure(EntityTypeBuilder<Instrument> builder)
    {
        builder.HasIndex(x => x.Ticker);

        builder.HasIndex(x => x.Isin).IsUnique();

        builder.HasIndex(x => x.Figi).IsUnique();

        builder.Property(x => x.Name).HasMaxLength(120);

        builder.Property(x => x.Ticker).HasMaxLength(16);

        builder.Property(x => x.Isin).HasMaxLength(12);

        builder.Property(x => x.Figi).HasMaxLength(32);

        builder.Property(x => x.Exchange).HasMaxLength(50);

        builder.Property(x => x.Country).HasMaxLength(100);

        builder.Property(x => x.CurrencyId).HasMaxLength(3);

        builder.Property(x => x.IsTrading).HasDefaultValue(true);

        builder.Property(x => x.Price).HasPrecision(18, 4);

        builder.Property(x => x.CreatedAt).ValueGeneratedOnAdd();

        builder.Property(x => x.UpdatedAt);
    }
}
