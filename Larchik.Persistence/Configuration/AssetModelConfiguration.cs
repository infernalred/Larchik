using Larchik.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Larchik.Persistence.Configuration;

public class AssetModelConfiguration : IEntityTypeConfiguration<Asset>
{
    public void Configure(EntityTypeBuilder<Asset> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.StockId).IsRequired();
        builder.Property(x => x.Quantity).IsRequired();

        builder.HasIndex(x => new {x.AccountId, x.StockId}).IsUnique();
    }
}