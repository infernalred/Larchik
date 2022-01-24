using Larchik.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Larchik.Persistence.Configuration;

public class StockTypeModelConfiguration : IEntityTypeConfiguration<StockType>
{
    public void Configure(EntityTypeBuilder<StockType> builder)
    {
        throw new NotImplementedException();
    }
}