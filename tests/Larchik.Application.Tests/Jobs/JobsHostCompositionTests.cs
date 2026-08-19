using Larchik.Application.FxRates.SyncCbrFxRates;
using Larchik.Application.Prices.SyncMoexPrices;
using Larchik.Application.Prices.SyncTbankPrices;
using Larchik.Application.Stocks.SyncTbankInstrumentInfo;
using Larchik.Application.Contracts;
using Larchik.Application.MarketDataImports.Processing;
using Larchik.Infrastructure.Jobs;
using Larchik.Infrastructure.MarketDataImports;
using Larchik.Infrastructure.Recalculation;
using Larchik.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Larchik.Application.Tests.Jobs;

public sealed class JobsHostCompositionTests
{
    [Theory]
    [InlineData("Development", true)]
    [InlineData("Local", true)]
    [InlineData("Production", false)]
    [InlineData(null, false)]
    public void ShouldUseConsoleBootstrapLogging_DependsOnEnvironment(string? environmentName, bool expected)
    {
        var actual = JobsHostComposition.ShouldUseConsoleBootstrapLogging(environmentName);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void ConfigureServices_RegistersJobsHostDependencies()
    {
        var services = new ServiceCollection();
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=test;Username=test;Password=test",
                ["BackgroundJobs:Enabled"] = "true"
            })
            .Build();

        JobsHostComposition.ConfigureServices(services, configuration);

        Assert.Contains(services, x => x.ServiceType == typeof(SyncCbrFxRatesCommandHandler));
        Assert.Contains(services, x => x.ServiceType == typeof(SyncMoexPricesCommandHandler));
        Assert.Contains(services, x => x.ServiceType == typeof(SyncTbankPricesCommandHandler));
        Assert.Contains(services, x => x.ServiceType == typeof(SyncTbankInstrumentInfoCommandHandler));
        Assert.Contains(services, x => x.ServiceType == typeof(IPortfolioReconciliationReportService));
        Assert.Contains(services, x => x.ServiceType == typeof(IPortfolioRecalcService) && x.ImplementationType == typeof(PortfolioRecalcService));
        Assert.Contains(services, x => x.ServiceType == typeof(DbContextOptions<LarchikContext>));
        Assert.Contains(services, x => x.ServiceType == typeof(IBackgroundJobHandler));
        Assert.Contains(services, x => x.ServiceType == typeof(IJobRunPlanner));
        Assert.Contains(services, x => x.ServiceType == typeof(ProcessMarketDataImportCommandHandler));
        Assert.Contains(services, x => x.ServiceType == typeof(IMarketDataImportSource) && x.ImplementationType == typeof(MoexMarketDataImportSource));
        Assert.Contains(services, x => x.ServiceType == typeof(IMarketDataImportSource) && x.ImplementationType == typeof(TbankMarketDataImportSource));
        Assert.Contains(services, x => x.ServiceType == typeof(IHostedService) && x.ImplementationType == typeof(MarketDataImportOutboxPublisherService));
        Assert.Contains(services, x => x.ServiceType == typeof(IHostedService) && x.ImplementationType == typeof(MarketDataImportConsumerService));
    }

    [Fact]
    public void ProductionConfig_DoesNotExcludeRussianTbankPriceSourceInstruments()
    {
        var repoRoot = FindRepoRoot();
        var configuration = new ConfigurationBuilder()
            .SetBasePath(repoRoot)
            .AddJsonFile(Path.Combine("src", "Larchik.Jobs", "appsettings.json"))
            .AddJsonFile(Path.Combine("src", "Larchik.Jobs", "appsettings.Production.json"))
            .Build();
        var services = new ServiceCollection();

        services.Configure<BackgroundJobsOptions>(configuration.GetSection("BackgroundJobs"));

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<BackgroundJobsOptions>>().Value;

        Assert.DoesNotContain("RU", options.TbankPricesDaily.CountryExclusions, StringComparer.OrdinalIgnoreCase);
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Larchik.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Repository root was not found.");
    }
}
