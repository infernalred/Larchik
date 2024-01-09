using Larchik.Domain.Enum;
using Larchik.Persistence.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Larchik.Persistence.Configuration;

public class StockModelConfiguration : IEntityTypeConfiguration<Stock>
{
    public void Configure(EntityTypeBuilder<Stock> builder)
    {
        builder.HasKey(x => x.Ticker);

        builder.Property(x => x.Ticker).IsRequired().HasMaxLength(8);
        
        builder.Property(x => x.Figi).IsRequired().HasMaxLength(12);
        
        builder.Property(x => x.CompanyName).IsRequired().HasMaxLength(60);
        
        builder.Property(x => x.TypeId).IsRequired();
        
        builder.Property(x => x.Type).HasDefaultValue(StockKind.Share);
        
        builder.Property(x => x.CurrencyId);
        
        builder.Property(x => x.SectorId).HasMaxLength(60);
        
        builder.Property(x => x.CreatedAt).ValueGeneratedOnAdd();

        builder.Property(x => x.UpdatedAt).ValueGeneratedOnAddOrUpdate();
    }
}