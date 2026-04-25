using Larchik.Application.Brokers.GetBrokers;
using Larchik.Application.Categories.GetCategories;
using Larchik.Application.Currencies.GetCurrencies;
using Larchik.Application.Operations.ImportBroker;
using Larchik.Application.ReferenceData.GetCountries;
using Larchik.Application.ReferenceData.GetExchanges;
using Larchik.Persistence.Context;
using Larchik.Persistence.Entities;
using Larchik.Application.Tests.TestInfrastructure;
using Xunit;

namespace Larchik.Application.Tests.ReferenceData;

public sealed class ReferenceDataQueryHandlersTests
{
    [Fact]
    public async Task GetBrokers_ReturnsNameSortedList_AndMarksSupportedImports()
    {
        await using var harness = new ReferenceDataHarness();
        harness.Context.Brokers.Add(new Broker
        {
            Id = Guid.NewGuid(),
            Code = null,
            Name = "ZZ Custom",
            Country = "Test"
        });
        await harness.Context.SaveChangesAsync();

        var handler = new GetBrokersQueryHandler(
            harness.Context,
            [new FakeBrokerReportParser("tbank"), new FakeBrokerReportParser("SBER")]);

        var result = await handler.Handle(new GetBrokersQuery(), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error);
        var brokers = result.Value!;
        Assert.Equal(brokers.OrderBy(x => x.Name, StringComparer.Ordinal).Select(x => x.Id), brokers.Select(x => x.Id));

        var tbank = Assert.Single(brokers, x => string.Equals(x.Code, "tbank", StringComparison.OrdinalIgnoreCase));
        Assert.True(tbank.SupportsImport);

        var sber = Assert.Single(brokers, x => string.Equals(x.Code, "sber", StringComparison.OrdinalIgnoreCase));
        Assert.True(sber.SupportsImport);

        var custom = Assert.Single(brokers, x => x.Name == "ZZ Custom");
        Assert.False(custom.SupportsImport);
    }

    [Fact]
    public async Task GetCategories_ReturnsIdSortedList()
    {
        await using var harness = new ReferenceDataHarness();
        var handler = new GetCategoriesQueryHandler(harness.Context);

        var result = await handler.Handle(new GetCategoriesQuery(), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error);
        var categories = result.Value!;
        Assert.Equal(categories.OrderBy(x => x.Id).Select(x => x.Id), categories.Select(x => x.Id));
    }

    [Fact]
    public async Task GetCurrencies_ReturnsCodeSortedList()
    {
        await using var harness = new ReferenceDataHarness();
        harness.Context.Currencies.AddRange(
            new Currency { Id = "JPY" },
            new Currency { Id = "AUD" });
        await harness.Context.SaveChangesAsync();

        var handler = new GetCurrenciesQueryHandler(harness.Context);
        var result = await handler.Handle(new GetCurrenciesQuery(), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error);
        var currencies = result.Value!;
        Assert.Equal(currencies.OrderBy(x => x.Id, StringComparer.Ordinal).Select(x => x.Id), currencies.Select(x => x.Id));
    }

    [Fact]
    public async Task GetCountries_ReturnsNameSortedList()
    {
        await using var harness = new ReferenceDataHarness();
        var handler = new GetCountriesQueryHandler(harness.Context);

        var result = await handler.Handle(new GetCountriesQuery(), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error);
        var countries = result.Value!;
        Assert.Contains(countries, x => x.Id == "RU");
        Assert.Equal(countries.OrderBy(x => x.Name, StringComparer.Ordinal).Select(x => x.Id), countries.Select(x => x.Id));
    }

    [Fact]
    public async Task GetExchanges_ReturnsNameSortedList()
    {
        await using var harness = new ReferenceDataHarness();
        var handler = new GetExchangesQueryHandler(harness.Context);

        var result = await handler.Handle(new GetExchangesQuery(), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error);
        var exchanges = result.Value!;
        Assert.Contains(exchanges, x => x.Id == "MOEX");
        Assert.DoesNotContain(exchanges, x => x.Id == "TQCB");
        Assert.Equal(exchanges.OrderBy(x => x.Name, StringComparer.Ordinal).Select(x => x.Id), exchanges.Select(x => x.Id));
    }

    private sealed class ReferenceDataHarness : IAsyncDisposable
    {
        private readonly SqliteTestDatabase database;

        public ReferenceDataHarness()
        {
            database = SqliteTestContextFactory.Create();
            Context = database.Context;
        }

        public LarchikContext Context { get; }

        public async ValueTask DisposeAsync()
        {
            await database.DisposeAsync();
        }
    }

    private sealed class FakeBrokerReportParser(string code) : IBrokerReportParser
    {
        public string Code { get; } = code;

        public Task<BrokerReportParseResult> ParseAsync(Stream fileStream, string fileName, CancellationToken cancellationToken) =>
            Task.FromResult(new BrokerReportParseResult([], []));
    }
}
