using Larchik.Persistence.Enum;
using Larchik.Persistence.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Larchik.Persistence.Configuration;

public class OperationModelConfiguration : IEntityTypeConfiguration<Operation>
{
    public void Configure(EntityTypeBuilder<Operation> builder)
    {
        builder.Property(x => x.Kind).HasDefaultValue(OperationKind.Add);
        builder.Property(x => x.CurrencyId).HasMaxLength(3);
    }
}