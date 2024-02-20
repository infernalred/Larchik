using System.Text;
using Larchik.API.Services;
using Larchik.Domain;
using Larchik.Infrastructure.Security;
using Larchik.Persistence.Context;
using Larchik.Persistence.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;

namespace Larchik.API.Configuration;

public static class ConfigurationSecurityExtensions
{
    public static IServiceCollection AddSecurityServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddIdentityCore<AppUser>(opt =>
            {
                opt.Password.RequireNonAlphanumeric = false;
                opt.User.RequireUniqueEmail = true;
            })
            .AddEntityFrameworkStores<DataContext>()
            .AddSignInManager<SignInManager<AppUser>>()
            .AddDefaultTokenProviders();
        //.AddRoles<AppRole>();

        // services.AddIdentity<AppUser, AppRole>()
        //     .AddEntityFrameworkStores<DataContext>()
        //     .AddDefaultTokenProviders();;

        var key = new SymmetricSecurityKey(Encoding.UTF8
            .GetBytes(configuration["TokenKey"] ?? throw new InvalidOperationException("TokenKey not found")));

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(opt =>
            {
                opt.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = key,
                    ValidateAudience = false,
                    ValidateIssuer = false,
                    ValidateLifetime = true
                };
            });

        // services.AddAuthorizationBuilder()
        //     .AddPolicy("IsAccountOwner", policy => 
        //         { policy.Requirements.Add(new IsAccountOwnerRequirement()); })
        //     .AddPolicy("IsDealOwner", policy => 
        //         { policy.Requirements.Add(new IsDealOwnerRequirement()); });

        services.AddTransient<IAuthorizationHandler, IsAccountOwnerRequirementHandler>();
        //services.AddTransient<IAuthorizationHandler, IsDealOwnerRequirementRequirementHandler>();
        services.AddScoped<ITokenService, TokenService>();

        return services;
    }
}