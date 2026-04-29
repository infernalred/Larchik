using Larchik.Application.FxRates.SyncCbrFxRates;
using Larchik.Application.Prices.SyncMoexPrices;
using Larchik.Application.Prices.SyncTbankPrices;
using Larchik.Application.Stocks.SyncTbankInstrumentInfo;
using Larchik.Application.Contracts;
using Larchik.Infrastructure.Jobs;
using Larchik.Infrastructure.Recalculation;
using Larchik.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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
    }
}
