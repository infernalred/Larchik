using Larchik.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Larchik.Persistence.Configuration;

public class CashTransactionModelConfiguration : IEntityTypeConfiguration<CashTransaction>
{
    public void Configure(EntityTypeBuilder<CashTransaction> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.OperationId).IsRequired();
        builder.Property(x => x.CashId).IsRequired();
    }
}