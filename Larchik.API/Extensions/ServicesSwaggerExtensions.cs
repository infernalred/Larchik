using NSwag;
using NSwag.Generation.Processors.Security;

namespace Larchik.API.Extensions;

public static class ServicesSwaggerExtensions
{
    public static IServiceCollection AddSwaggerServices(this IServiceCollection services)
    {
        services.AddOpenApiDocument(options =>
        {
            options.Title = "Larchik API v2.0";
            options.Version = "v2";
            options.AddSecurity("Bearer", Enumerable.Empty<string>(), new()
            {
                Type = OpenApiSecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                Description = "JWT Authorization header using the Bearer scheme."
            });
            options.OperationProcessors.Add(new AspNetCoreOperationSecurityScopeProcessor("Bearer"));
        });

        return services;
    }
}
