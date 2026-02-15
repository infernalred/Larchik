using Larchik.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Larchik.Persistence.Configuration;

public class JobRunModelConfiguration : IEntityTypeConfiguration<JobRun>
{
    public void Configure(EntityTypeBuilder<JobRun> builder)
    {
        builder.Property(x => x.DedupKey).IsRequired().HasMaxLength(200);
        builder.Property(x => x.PayloadJson).IsRequired().HasColumnType("jsonb");
        builder.Property(x => x.LockedBy).HasMaxLength(120);
        builder.Property(x => x.LastError).HasMaxLength(4000);
        builder.Property(x => x.MaxAttempts).HasDefaultValue(5);
        builder.Property(x => x.CreatedAt).ValueGeneratedOnAdd();
        builder.Property(x => x.UpdatedAt).ValueGeneratedOnAddOrUpdate();

        builder.HasIndex(x => x.DedupKey).IsUnique();
        builder.HasIndex(x => new { x.Status, x.AvailableAt });
        builder.HasIndex(x => x.LockedUntilAt);
        builder.HasIndex(x => new { x.JobDefinitionId, x.CreatedAt });

        builder.HasOne(x => x.JobDefinition)
            .WithMany(x => x.Runs)
            .HasForeignKey(x => x.JobDefinitionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
