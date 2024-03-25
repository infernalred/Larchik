using Larchik.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Larchik.Persistence.Configuration;

public class StockModelConfiguration : IEntityTypeConfiguration<Stock>
{
    public void Configure(EntityTypeBuilder<Stock> builder)
    {
        builder.HasIndex(x => x.Ticker);

        builder.HasIndex(x => x.Isin).IsUnique();

        builder.Property(x => x.Name).HasMaxLength(60);

        builder.Property(x => x.Ticker).HasMaxLength(8);

        builder.Property(x => x.Isin).HasMaxLength(12);

        builder.Property(x => x.CurrencyId).HasMaxLength(3);

        builder.Property(x => x.CreatedAt).ValueGeneratedOnAdd();

        builder.Property(x => x.UpdatedAt).ValueGeneratedOnAddOrUpdate();
    }
}