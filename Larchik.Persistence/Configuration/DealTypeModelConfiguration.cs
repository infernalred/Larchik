using Larchik.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Larchik.Persistence.Configuration;

public class DealTypeModelConfiguration : IEntityTypeConfiguration<DealType>
{
    public void Configure(EntityTypeBuilder<DealType> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Code).IsRequired().HasMaxLength(25);
    }
}