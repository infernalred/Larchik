using Larchik.Persistence.Constants;
using Larchik.Persistence.Entities;
using Microsoft.AspNetCore.Identity;

namespace Larchik.API.Services;

public static class IdentitySeeder
{
    public static async Task SeedAsync(IServiceProvider services, IConfiguration configuration, CancellationToken cancellationToken = default)
    {
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger(nameof(IdentitySeeder));
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        var userManager = services.GetRequiredService<UserManager<AppUser>>();

        await EnsureRoleAsync(roleManager, Roles.Admin, logger, cancellationToken);
        await EnsureRoleAsync(roleManager, Roles.User, logger, cancellationToken);

        var adminEmail = configuration["Admin:Email"];
        var adminPassword = configuration["Admin:Password"];

        if (string.IsNullOrWhiteSpace(adminEmail) || string.IsNullOrWhiteSpace(adminPassword))
        {
            return;
        }

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
                return;
            }
        }

        if (!await userManager.IsInRoleAsync(adminUser, Roles.Admin))
        {
            await userManager.AddToRoleAsync(adminUser, Roles.Admin);
        }
    }

    private static async Task EnsureRoleAsync(
        RoleManager<IdentityRole<Guid>> roleManager,
        string roleName,
        ILogger logger,
        CancellationToken cancellationToken)
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
