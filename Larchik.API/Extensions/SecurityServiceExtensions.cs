using System.Text;
using Larchik.Persistence.Context;
using Larchik.Persistence.Entities;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace Larchik.API.Extensions;

public static class SecurityServiceExtensions
{
    public static IServiceCollection AddSecurityServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddIdentityCore<AppUser>(opt =>
            {
                opt.Password.RequireNonAlphanumeric = false;
                opt.User.RequireUniqueEmail = true;
            })
            .AddEntityFrameworkStores<LarchikContext>();

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

        return services;
    }
}