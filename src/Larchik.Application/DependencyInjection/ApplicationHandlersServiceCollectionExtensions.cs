using Larchik.Application.Currencies.GetCurrencies;
using Microsoft.Extensions.DependencyInjection;

namespace Larchik.Application.DependencyInjection;

/// <summary>
/// Registers application-layer command/query handlers as scoped services (direct DI, no mediator).
/// </summary>
public static class ApplicationHandlersServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationHandlers(this IServiceCollection services)
    {
        var assembly = typeof(GetCurrenciesQueryHandler).Assembly;
        var handlerTypes = assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false }
                        && t.Name.EndsWith("Handler", StringComparison.Ordinal));

        foreach (var type in handlerTypes)
        {
            services.AddScoped(type);
        }

        return services;
    }
}
