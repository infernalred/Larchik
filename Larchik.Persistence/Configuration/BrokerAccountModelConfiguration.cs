using Larchik.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Larchik.Persistence.Configuration;

public class BrokerAccountModelConfiguration : IEntityTypeConfiguration<Account>
{
    public void Configure(EntityTypeBuilder<Account> builder)
    {
        builder.HasKey(x => x.Id);
        
        builder.Property(x => x.UserId).IsRequired();
        builder.Property(x => x.BrokerId).IsRequired();
    }
}