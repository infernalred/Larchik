using Larchik.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Larchik.Persistence.Configuration;

public class MarketDataImportRequestModelConfiguration : IEntityTypeConfiguration<MarketDataImportRequest>
{
    public void Configure(EntityTypeBuilder<MarketDataImportRequest> builder)
    {
        builder.Property(x => x.Source).HasConversion<string>().HasMaxLength(16);
        builder.Property(x => x.Isin).IsRequired().HasMaxLength(12);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.IdempotencyKey).HasMaxLength(120);
        builder.Property(x => x.SourceInstrumentCode).HasMaxLength(36);
        builder.Property(x => x.SourceBoard).HasMaxLength(16);
        builder.Property(x => x.SourceEngine).HasMaxLength(16);
        builder.Property(x => x.SourceMarket).HasMaxLength(32);
        builder.Property(x => x.LastError).HasMaxLength(4000);
        builder.HasCreatedAt(x => x.CreatedAt);
        builder.HasUpdatedAt(x => x.UpdatedAt);

        builder.HasIndex(x => x.IdempotencyKey).IsUnique();
        builder.HasIndex(x => new { x.Status, x.CreatedAt });
        builder.HasIndex(x => new { x.Source, x.Isin, x.CreatedAt });

        builder.HasOne(x => x.Instrument)
            .WithMany()
            .HasForeignKey(x => x.InstrumentId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
