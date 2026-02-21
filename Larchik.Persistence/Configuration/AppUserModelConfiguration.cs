using Larchik.Persistence.Entities;
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
            NormalizedUserName = "ADMIN",
            Email = "admin@test.com",
            NormalizedEmail = "ADMIN@TEST.COM",
            EmailConfirmed = true,
            SecurityStamp = "f3359b6674a7407793f4e0371c477b60",
            ConcurrencyStamp = "c53a3830-3f86-4505-bcdb-1d2d2f87c006",
            PasswordHash = "AQAAAAIAAYagAAAAELetNQlOXe6IFms9D+H9cktwcVgon6E7yho5xMfUV8vbI8lfSldk14mcajcwvxJeBQ=="
        };

        builder.HasData(user);
    }
}
