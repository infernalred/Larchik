using Larchik.Application.FxRates.SyncCbrFxRates;
using Larchik.Application.Prices.SyncMoexPrices;
using Larchik.Application.Prices.SyncTbankPrices;
using Larchik.Application.Stocks.SyncTbankInstrumentInfo;
using Larchik.Infrastructure.Jobs;
using Larchik.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Serilog;

var bootstrapEnvironment =
    Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ??
    Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");

var bootstrapLoggerConfiguration = new LoggerConfiguration();
if (string.Equals(bootstrapEnvironment, "Development", StringComparison.OrdinalIgnoreCase) ||
    string.Equals(bootstrapEnvironment, "Local", StringComparison.OrdinalIgnoreCase))
{
    bootstrapLoggerConfiguration = bootstrapLoggerConfiguration.WriteTo.Console();
}

Log.Logger = bootstrapLoggerConfiguration.CreateBootstrapLogger();

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
            services.AddScoped<SyncTbankPricesCommandHandler>();
            services.AddScoped<SyncTbankInstrumentInfoCommandHandler>();

            services.AddBackgroundJobs(context.Configuration);
        })
        .Build();

    var options = host.Services.GetRequiredService<IOptionsMonitor<BackgroundJobsOptions>>().CurrentValue;
    var startupLogger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("JobsHost");
    startupLogger.LogInformation(
        "Jobs host started. Waiting for timers. BackgroundJobs enabled: {Enabled}. Scheduler poll: {SchedulerPollSeconds}s. Executor poll: {ExecutorPollSeconds}s. Batch size: {ExecutorBatchSize}.",
        options.Enabled,
        options.SchedulerPollSeconds,
        options.ExecutorPollSeconds,
        options.ExecutorBatchSize);

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
