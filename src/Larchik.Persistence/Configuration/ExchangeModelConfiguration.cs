using Larchik.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Larchik.Persistence.Configuration;

public class ExchangeModelConfiguration : IEntityTypeConfiguration<Exchange>
{
    public void Configure(EntityTypeBuilder<Exchange> builder)
    {
        builder.Property(x => x.Id)
            .HasMaxLength(16)
            .IsRequired();

        builder.Property(x => x.Name)
            .HasMaxLength(100)
            .IsRequired();

        builder.HasData(
            new Exchange { Id = "MOEX", Name = "Moscow Exchange" },
            new Exchange { Id = "SPBX", Name = "SPB Exchange" },
            new Exchange { Id = "NYSE", Name = "New York Stock Exchange" },
            new Exchange { Id = "NASDAQ", Name = "Nasdaq" },
            new Exchange { Id = "LSE", Name = "London Stock Exchange" },
            new Exchange { Id = "HKEX", Name = "Hong Kong Exchange" },
            new Exchange { Id = "TEST", Name = "Test Exchange" });
    }
}
