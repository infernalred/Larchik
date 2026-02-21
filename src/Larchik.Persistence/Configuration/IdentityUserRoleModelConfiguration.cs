using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Larchik.Persistence.Configuration;

public class IdentityUserRoleModelConfiguration : IEntityTypeConfiguration<IdentityUserRole<Guid>>
{
    public void Configure(EntityTypeBuilder<IdentityUserRole<Guid>> builder)
    {
        var userRole = new IdentityUserRole<Guid>
        {
            UserId = Guid.Parse("7e89d7d2-21e2-40ce-bef2-58c3b9408abb"),
            RoleId = Guid.Parse("e5165cd8-4c41-4cc2-8aad-47b879f9da38")
        };

        builder.HasData(userRole);
    }
}