namespace Larchik.API.Extensions;

public static class CorsExtensions
{
    public static IServiceCollection AddCorsServices(this IServiceCollection services, IConfiguration configuration)
    {
        var origins = configuration.GetSection("Cors")?.GetSection("Origins")?.Value?
            .Split(",", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        services.AddCors(opt =>
        {
            opt.AddPolicy("CorsPolicy", builder =>
            {
                builder.AllowAnyHeader();
                builder.AllowAnyMethod();

                if (origins is { Length: > 0 })
                {
                    if (origins.Contains("*"))
                    {
                        builder.AllowAnyOrigin();
                    }
                    else
                    {
                        builder.WithOrigins(origins).AllowCredentials();
                    }
                }
                else
                {
                    builder.AllowAnyOrigin();
                }

                builder.WithExposedHeaders("WWW-Authenticate", "Pagination", "Content-Disposition");
            });
        });
        
        return services;
    }
}
