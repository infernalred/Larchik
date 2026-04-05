using Larchik.Application.Contracts;
using Larchik.Infrastructure.Security;
using Larchik.Persistence.Context;
using Larchik.Persistence.Entities;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Larchik.API.Services;

namespace Larchik.API.Extensions;

public static class SecurityServiceExtensions
{
    public static IServiceCollection AddSecurityServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddIdentityCore<AppUser>(opt =>
            {
                opt.Password.RequireNonAlphanumeric = false;
                opt.Password.RequiredLength = 8;
                opt.Password.RequireUppercase = true;
                opt.SignIn.RequireConfirmedEmail = true;
                opt.User.RequireUniqueEmail = true;
                opt.Lockout.AllowedForNewUsers = true;
                opt.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
                opt.Lockout.MaxFailedAccessAttempts = 5;
            })
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<LarchikContext>()
            .AddSignInManager()
            .AddDefaultTokenProviders();

        var keysPath = ResolveDataProtectionKeysPath(configuration);
        Directory.CreateDirectory(keysPath);

        services.AddDataProtection()
            .PersistKeysToFileSystem(new DirectoryInfo(keysPath))
            .SetApplicationName("Larchik");

        services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = IdentityConstants.ApplicationScheme;
                options.DefaultChallengeScheme = IdentityConstants.ApplicationScheme;
                options.DefaultSignInScheme = IdentityConstants.ApplicationScheme;
            })
            .AddIdentityCookies();

        services.ConfigureApplicationCookie(cookie =>
        {
            cookie.Cookie.Name = "__Host-larchik-auth";
            cookie.Cookie.Path = "/";
            cookie.Cookie.HttpOnly = true;
            cookie.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            cookie.Cookie.SameSite = SameSiteMode.Lax;
            cookie.SlidingExpiration = true;
            cookie.ExpireTimeSpan = TimeSpan.FromDays(7);
        });

        services.AddAntiforgery(options =>
        {
            options.Cookie.Name = "__Host-larchik-af";
            options.Cookie.Path = "/";
            options.Cookie.HttpOnly = true;
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.HeaderName = "X-XSRF-TOKEN";
        });

        services.AddScoped<IUserAccessor, UserAccessor>();
        services.AddScoped<IEmailSender, LoggingEmailSender>();

        return services;
    }

    private static string ResolveDataProtectionKeysPath(IConfiguration configuration)
    {
        var configuredPath = configuration["DataProtection:KeysPath"];
        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            return configuredPath;
        }

        return Path.Combine(Path.GetTempPath(), "larchik-dpkeys");
    }
}
