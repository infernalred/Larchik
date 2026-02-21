using Larchik.Application.Contracts;
using Larchik.Infrastructure.Security;
using Larchik.Persistence.Context;
using Larchik.Persistence.Entities;
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

        services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = IdentityConstants.ApplicationScheme;
                options.DefaultChallengeScheme = IdentityConstants.ApplicationScheme;
                options.DefaultSignInScheme = IdentityConstants.ApplicationScheme;
            })
            .AddIdentityCookies(o =>
            {
                o.ApplicationCookie?.Configure(cookie =>
                {
                    cookie.Cookie.Name = "__Host-larchik-auth";
                    cookie.Cookie.HttpOnly = true;
                    cookie.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                    cookie.Cookie.SameSite = SameSiteMode.None;
                    cookie.SlidingExpiration = true;
                    cookie.ExpireTimeSpan = TimeSpan.FromMinutes(30);
                });
            });

        services.AddAntiforgery(options =>
        {
            options.Cookie.Name = "__Host-larchik-af";
            options.Cookie.HttpOnly = true;
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            options.Cookie.SameSite = SameSiteMode.None;
            options.HeaderName = "X-XSRF-TOKEN";
        });

        services.AddScoped<IUserAccessor, UserAccessor>();
        services.AddScoped<IEmailSender, LoggingEmailSender>();

        return services;
    }
}
