using Larchik.Persistence.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Larchik.Persistence.Configuration;

public class AppUserModelConfiguration : IEntityTypeConfiguration<AppUser>
{
    public void Configure(EntityTypeBuilder<AppUser> builder)
    {
        var user = new AppUser
        {
            Id = Guid.Parse("7e89d7d2-21e2-40ce-bef2-58c3b9408abb"),
            UserName = "admin",
            NormalizedUserName = "admin".ToUpper(),
            Email = "admin@test.com",
            NormalizedEmail = "admin@test.com".ToUpper(),
            EmailConfirmed = true
        };

        var ph = new PasswordHasher<AppUser>();
        user.PasswordHash = ph.HashPassword(user, "Password!!!123");

        builder.HasData(
            user
        );
    }
}