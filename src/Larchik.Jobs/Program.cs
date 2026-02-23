using Larchik.Application.FxRates.SyncCbrFxRates;
using Larchik.Application.Prices.SyncMoexPrices;
using Larchik.Infrastructure.Jobs;
using Larchik.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var host = Host.CreateDefaultBuilder(args)
        .UseSerilog((context, services, configuration) =>
            configuration
                .ReadFrom.Configuration(context.Configuration)
                .ReadFrom.Services(services)
                .Enrich.FromLogContext())
        .ConfigureServices((context, services) =>
        {
            services.AddDbContext<LarchikContext>(opt =>
            {
                opt.UseNpgsql(context.Configuration.GetConnectionString("DefaultConnection"),
                    o => o.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery));
                opt.UseSnakeCaseNamingConvention();
            });

            services.AddHttpClient();

            // Jobs host only needs sync handlers used by background job adapters.
            services.AddScoped<SyncCbrFxRatesCommandHandler>();
            services.AddScoped<SyncMoexPricesCommandHandler>();

            services.AddBackgroundJobs(context.Configuration);
        })
        .Build();

    await host.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Jobs host terminated unexpectedly");
    throw;
}
finally
{
    await Log.CloseAndFlushAsync();
}
