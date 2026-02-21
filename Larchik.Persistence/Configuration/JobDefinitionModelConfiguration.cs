using Larchik.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Larchik.Persistence.Configuration;

public class JobDefinitionModelConfiguration : IEntityTypeConfiguration<JobDefinition>
{
    public void Configure(EntityTypeBuilder<JobDefinition> builder)
    {
        builder.Property(x => x.Name).IsRequired().HasMaxLength(120);
        builder.Property(x => x.JobType).IsRequired().HasMaxLength(120);
        builder.Property(x => x.ScheduleValue).IsRequired().HasMaxLength(50);
        builder.Property(x => x.MaxAttempts).HasDefaultValue(5);
        builder.Property(x => x.RetryDelayMinutes).HasDefaultValue(15);
        builder.Property(x => x.LockTimeoutMinutes).HasDefaultValue(5);
        builder.Property(x => x.CreatedAt);
        builder.Property(x => x.UpdatedAt);

        builder.HasIndex(x => x.Name).IsUnique();
        builder.HasIndex(x => new { x.IsEnabled, x.NextRunAt });
    }
}
