using Larchik.Application.FxRates.SyncCbrFxRates;
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

            services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(SyncCbrFxRatesCommand).Assembly));
            services.AddHttpClient();
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
