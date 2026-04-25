using Larchik.Application.Common.Paging;
using Larchik.Application.Stocks.GetAdminInstruments;
using Larchik.Application.Tests.TestInfrastructure;
using Larchik.Persistence.Context;
using Larchik.Persistence.Entities;
using Xunit;

namespace Larchik.Application.Tests.Stocks;

public sealed class GetAdminInstrumentsQueryHandlerTests
{
    [Fact]
    public async Task Handle_WhenIsTradingFilterIsTrue_ReturnsOnlyTradingInstruments()
    {
        await using var harness = new StocksHarness();
        harness.Context.Instruments.AddRange(
            CreateInstrument("GAZP", isTrading: true),
            CreateInstrument("MANUAL", isTrading: false));
        await harness.Context.SaveChangesAsync();

        var handler = new GetAdminInstrumentsQueryHandler(harness.Context);

        var result = await handler.Handle(
            new GetAdminInstrumentsQuery(null, null, true, new PageQuery { Page = 1, PageSize = 50 }),
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error);
        var instruments = result.Value!;
        var instrument = Assert.Single(instruments.Items);
        Assert.Equal("GAZP", instrument.Ticker);
        Assert.True(instrument.IsTrading);
    }

    [Fact]
    public async Task Handle_WhenIsTradingFilterIsNotSpecified_ReturnsTradingAndNonTradingInstruments()
    {
        await using var harness = new StocksHarness();
        harness.Context.Instruments.AddRange(
            CreateInstrument("GAZP", isTrading: true),
            CreateInstrument("MANUAL", isTrading: false));
        await harness.Context.SaveChangesAsync();

        var handler = new GetAdminInstrumentsQueryHandler(harness.Context);

        var result = await handler.Handle(
            new GetAdminInstrumentsQuery(null, null, null, new PageQuery { Page = 1, PageSize = 50 }),
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error);
        var tickers = result.Value!.Items.Select(x => x.Ticker).ToArray();
        Assert.Equal(["GAZP", "MANUAL"], tickers);
    }

    private static Instrument CreateInstrument(string ticker, bool isTrading) =>
        new()
        {
            Id = Guid.NewGuid(),
            Name = $"{ticker} instrument",
            Ticker = ticker,
            Type = InstrumentType.Equity,
            CurrencyId = "USD",
            CategoryId = 4,
            ExchangeId = "TEST",
            CountryId = "US",
            IsTrading = isTrading,
            CreatedBy = Guid.NewGuid(),
            UpdatedBy = Guid.NewGuid()
        };

    private sealed class StocksHarness : IAsyncDisposable
    {
        private readonly SqliteTestDatabase database = SqliteTestContextFactory.Create();

        public LarchikContext Context => database.Context;

        public async ValueTask DisposeAsync()
        {
            await database.DisposeAsync();
        }
    }
}
