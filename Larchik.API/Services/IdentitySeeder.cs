using Larchik.Persistence.Constants;
using Larchik.Persistence.Entities;
using Microsoft.AspNetCore.Identity;

namespace Larchik.API.Services;

public static class IdentitySeeder
{
    public static async Task SeedAsync(IServiceProvider services, IConfiguration configuration)
    {
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger(nameof(IdentitySeeder));
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        var userManager = services.GetRequiredService<UserManager<AppUser>>();

        await EnsureRoleAsync(roleManager, Roles.Admin, logger);
        await EnsureRoleAsync(roleManager, Roles.User, logger);

        var adminEmail = configuration["Admin:Email"];
        var adminPassword = configuration["Admin:Password"];

        if (!string.IsNullOrWhiteSpace(adminEmail) && !string.IsNullOrWhiteSpace(adminPassword))
        {
            var adminUser = await userManager.FindByEmailAsync(adminEmail);
            if (adminUser is null)
            {
                adminUser = new AppUser
                {
                    Email = adminEmail,
                    UserName = adminEmail,
                    EmailConfirmed = true
                };

                var createResult = await userManager.CreateAsync(adminUser, adminPassword);
                if (!createResult.Succeeded)
                {
                    logger.LogError("Failed to create admin user {Email}: {Errors}", adminEmail,
                        string.Join(", ", createResult.Errors.Select(e => e.Description)));
                }
            }

            if (adminUser is not null && !await userManager.IsInRoleAsync(adminUser, Roles.Admin))
            {
                await userManager.AddToRoleAsync(adminUser, Roles.Admin);
            }
        }

    }

    private static async Task EnsureRoleAsync(
        RoleManager<IdentityRole<Guid>> roleManager,
        string roleName,
        ILogger logger)
    {
        if (await roleManager.RoleExistsAsync(roleName)) return;

        var result = await roleManager.CreateAsync(new IdentityRole<Guid>(roleName));
        if (!result.Succeeded)
        {
            logger.LogError("Failed to create role {Role}: {Errors}", roleName,
                string.Join(", ", result.Errors.Select(e => e.Description)));
        }
    }
}
