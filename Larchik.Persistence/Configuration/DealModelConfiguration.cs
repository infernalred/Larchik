using Larchik.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Larchik.Persistence.Configuration;

public class DealModelConfiguration : IEntityTypeConfiguration<Deal>
{
    public void Configure(EntityTypeBuilder<Deal> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.TypeId).HasDefaultValue(1);
        builder.Property(x => x.CurrencyId).IsRequired();
    }
}