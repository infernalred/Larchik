using Larchik.Application.Currencies.GetCurrencies;
using Larchik.Persistence.Context;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.EntityFrameworkCore;

namespace Larchik.API.Extensions;

public static class ApplicationServiceExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContext<LarchikContext>(opt =>
        {
            opt.UseNpgsql(configuration.GetConnectionString("DefaultConnection"),
                o => o.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery));
            opt.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
        });

        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(GetCurrenciesQuery).Assembly));

        services.AddControllers(opt =>
        {
            var policy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
            opt.Filters.Add(new AuthorizeFilter(policy));
        });

        services.AddHttpContextAccessor();

        services.Configure<RouteOptions>(options => options.LowercaseUrls = true);

        return services;
    }
}