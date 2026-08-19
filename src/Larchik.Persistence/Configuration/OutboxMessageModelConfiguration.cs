using Larchik.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Larchik.Persistence.Configuration;

public class OutboxMessageModelConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.Property(x => x.MessageType).IsRequired().HasMaxLength(120);
        builder.Property(x => x.PayloadJson).IsRequired().HasColumnType("jsonb");
        builder.Property(x => x.LastError).HasMaxLength(4000);
        builder.Property(x => x.LockedBy).HasMaxLength(120);
        builder.HasCreatedAt(x => x.CreatedAt);
        builder.HasUpdatedAt(x => x.UpdatedAt);

        builder.HasIndex(x => new { x.PublishedAt, x.AvailableAt });
        builder.HasIndex(x => x.LockedUntilAt);
    }
}
