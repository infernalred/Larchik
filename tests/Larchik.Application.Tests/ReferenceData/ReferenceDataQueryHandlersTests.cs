using Larchik.Application.Brokers.GetBrokers;
using Larchik.Application.Categories.GetCategories;
using Larchik.Application.Currencies.GetCurrencies;
using Larchik.Application.Operations.ImportBroker;
using Larchik.Persistence.Context;
using Larchik.Persistence.Entities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
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

    private sealed class ReferenceDataHarness : IAsyncDisposable
    {
        private readonly SqliteConnection connection;

        public ReferenceDataHarness()
        {
            connection = new SqliteConnection("Data Source=:memory:");
            connection.Open();

            var options = new DbContextOptionsBuilder<LarchikContext>()
                .UseSqlite(connection)
                .Options;

            Context = new LarchikContext(options);
            Context.Database.EnsureCreated();
        }

        public LarchikContext Context { get; }

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            await connection.DisposeAsync();
        }
    }

    private sealed class FakeBrokerReportParser(string code) : IBrokerReportParser
    {
        public string Code { get; } = code;

        public Task<BrokerReportParseResult> ParseAsync(Stream fileStream, string fileName, CancellationToken cancellationToken) =>
            Task.FromResult(new BrokerReportParseResult([], []));
    }
}
