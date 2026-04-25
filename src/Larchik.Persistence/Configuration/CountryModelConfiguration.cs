using Larchik.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Larchik.Persistence.Configuration;

public class CountryModelConfiguration : IEntityTypeConfiguration<Country>
{
    public void Configure(EntityTypeBuilder<Country> builder)
    {
        builder.Property(x => x.Id)
            .HasMaxLength(2)
            .IsRequired();

        builder.Property(x => x.Name)
            .HasMaxLength(100)
            .IsRequired();

        builder.HasData(
            new Country { Id = "RU", Name = "Russia" },
            new Country { Id = "US", Name = "United States" },
            new Country { Id = "NL", Name = "Netherlands" },
            new Country { Id = "GB", Name = "United Kingdom" },
            new Country { Id = "DE", Name = "Germany" },
            new Country { Id = "CN", Name = "China" },
            new Country { Id = "HK", Name = "Hong Kong" },
            new Country { Id = "KZ", Name = "Kazakhstan" },
            new Country { Id = "CH", Name = "Switzerland" },
            new Country { Id = "IE", Name = "Ireland" });
    }
}
