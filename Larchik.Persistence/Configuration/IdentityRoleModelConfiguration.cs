using Larchik.Persistence.Constants;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Larchik.Persistence.Configuration;

public class IdentityRoleModelConfiguration : IEntityTypeConfiguration<IdentityRole<Guid>>
{
    public void Configure(EntityTypeBuilder<IdentityRole<Guid>> builder)
    {
        var admin = new IdentityRole<Guid>
        {
            Id = Guid.Parse("e5165cd8-4c41-4cc2-8aad-47b879f9da38"),
            Name = Roles.Admin,
            NormalizedName = Roles.Admin.ToUpper()
        };

        builder.HasData(
            admin
        );
    }
}