using Larchik.Application.FxRates.SyncCbrFxRates;
using Larchik.Application.Prices.SyncMoexPrices;
using Larchik.Application.Prices.SyncTbankPrices;
using Larchik.Application.Stocks.SyncTbankInstrumentInfo;
using Larchik.Application.Contracts;
using Larchik.Application.MarketDataImports.Processing;
using Larchik.Infrastructure.MarketDataImports;
using Larchik.Infrastructure.Recalculation;
using Larchik.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Larchik.Infrastructure.Jobs;

public static class JobsHostComposition
{
    public static bool ShouldUseConsoleBootstrapLogging(string? environmentName) =>
        string.Equals(environmentName, "Development", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(environmentName, "Local", StringComparison.OrdinalIgnoreCase);

    public static void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<LarchikContext>(options =>
        {
            options.UseNpgsql(
                configuration.GetConnectionString("DefaultConnection"),
                npgsql => npgsql.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery));
            options.UseSnakeCaseNamingConvention();
        });

        services.AddHttpClient();

        // Jobs host only needs sync handlers used by background job adapters.
        services.AddScoped<SyncCbrFxRatesCommandHandler>();
        services.AddScoped<SyncMoexPricesCommandHandler>();
        services.AddScoped<SyncTbankPricesCommandHandler>();
        services.AddScoped<SyncTbankInstrumentInfoCommandHandler>();
        services.AddScoped<ProcessMarketDataImportCommandHandler>();
        services.AddScoped<IMarketDataImportSource, MoexMarketDataImportSource>();
        services.AddScoped<IMarketDataImportSource, TbankMarketDataImportSource>();
        services.AddScoped<PortfolioReconciliationReportService>();
        services.AddScoped<IPortfolioReconciliationReportService, PortfolioReconciliationReportService>();
        services.AddScoped<IPortfolioRecalcService, PortfolioRecalcService>();

        services.Configure<MarketDataImportOptions>(configuration.GetSection(MarketDataImportOptions.SectionName));
        services.Configure<MarketDataImportSourceOptions>(configuration.GetSection(MarketDataImportSourceOptions.SectionName));
        services.Configure<RabbitMqOptions>(configuration.GetSection(RabbitMqOptions.SectionName));
        services.AddHostedService<MarketDataImportOutboxPublisherService>();
        services.AddHostedService<MarketDataImportConsumerService>();

        services.AddBackgroundJobs(configuration);
    }
}
