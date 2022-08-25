using Larchik.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Larchik.Persistence.Configuration;

public class StockTypeModelConfiguration : IEntityTypeConfiguration<StockType>
{
    public void Configure(EntityTypeBuilder<StockType> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Code).IsRequired().HasMaxLength(25);
    }
}