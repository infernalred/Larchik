using Larchik.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Larchik.Persistence.Configuration;

public class PortfolioModelConfiguration : IEntityTypeConfiguration<Portfolio>
{
    public void Configure(EntityTypeBuilder<Portfolio> builder)
    {
        builder.Property(x => x.Name).IsRequired().HasMaxLength(120);
        builder.HasCurrencyCode(x => x.ReportingCurrencyId, required: true);
        builder.HasCreatedAt(x => x.CreatedAt, generatedOnAdd: true);

        builder.HasOne(x => x.Broker)
            .WithMany(x => x.Portfolios)
            .HasForeignKey(x => x.BrokerId);

        builder.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId);
    }
}
