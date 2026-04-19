using Larchik.Application.Operations.ImportBroker;
using Larchik.Application.Portfolios.Valuation;
using Larchik.Persistence.Entities;
using Xunit;

namespace Larchik.Application.Tests.Portfolios;

public sealed class ValuationHelpersTests
{
    [Fact]
    public void HistoricalDataLookup_PrefersHigherPriorityPriceProvider_OnSameDate()
    {
        var instrumentId = Guid.NewGuid();
        var date = new DateTime(2026, 4, 20, 0, 0, 0, DateTimeKind.Utc);
        Price[] prices =
        [
            new Price
            {
                InstrumentId = instrumentId,
                Date = date,
                Value = 100m,
                CurrencyId = "RUB",
                Provider = "MOEX",
                CreatedAt = date
            },
            new Price
            {
                InstrumentId = instrumentId,
                Date = date,
                Value = 105m,
                CurrencyId = "RUB",
                Provider = "TBANK",
                CreatedAt = date
            }
        ];

        var lookup = new HistoricalDataLookup(prices, []);

        var price = lookup.GetPrice(instrumentId, date);

        Assert.NotNull(price);
        Assert.Equal(105m, price!.Value);
        Assert.Equal("TBANK", price.Provider);
    }

    [Fact]
    public void HistoricalDataLookup_PrefersHigherPriorityFxSource_OnSameDate()
    {
        var date = new DateTime(2026, 4, 20, 0, 0, 0, DateTimeKind.Utc);
        FxRate[] fxRates =
        [
            new FxRate
            {
                BaseCurrencyId = "USD",
                QuoteCurrencyId = "RUB",
                Date = date,
                Rate = 79m,
                Source = "CBR",
                CreatedAt = date
            },
            new FxRate
            {
                BaseCurrencyId = "USD",
                QuoteCurrencyId = "RUB",
                Date = date,
                Rate = 81m,
                Source = "MARKET_TBANK",
                CreatedAt = date
            }
        ];

        var lookup = new HistoricalDataLookup([], fxRates);

        var rate = lookup.GetRate("USD", "RUB", date);

        Assert.Equal(81m, rate);
        Assert.Equal(810m, lookup.Convert(10m, "USD", "RUB", date));
    }

    [Fact]
    public void InstrumentAccountingCurrencyHelper_UsesBaseCurrency_ForMixedOperationCurrencies()
    {
        var instrumentId = Guid.NewGuid();
        var instruments = new Dictionary<Guid, Instrument>
        {
            [instrumentId] = new()
            {
                Id = instrumentId,
                CurrencyId = "RUB",
                Type = InstrumentType.Equity,
                Name = "AAPL",
                Ticker = "AAPL"
            }
        };
        Operation[] operations =
        [
            new Operation { InstrumentId = instrumentId, CurrencyId = "USD" },
            new Operation { InstrumentId = instrumentId, CurrencyId = "RUB" }
        ];

        var result = InstrumentAccountingCurrencyHelper.Build(operations, instruments, "EUR");

        Assert.Equal("EUR", result[instrumentId]);
    }

    [Fact]
    public void BrokerCashLedgerHelper_UsesSettlementDate_ForConfirmedImportedSecurityOperation()
    {
        var operation = new Operation
        {
            InstrumentId = Guid.NewGuid(),
            TradeDate = new DateTime(2026, 4, 20, 0, 0, 0, DateTimeKind.Utc),
            SettlementDate = new DateTime(2026, 4, 22, 0, 0, 0, DateTimeKind.Utc),
            BrokerOperationKey = "v2:abc:000001"
        };

        var effectiveDate = BrokerCashLedgerHelper.GetCashEffectiveDate(operation);

        Assert.Equal(new DateTime(2026, 4, 22, 0, 0, 0, DateTimeKind.Utc), effectiveDate);
    }

    [Fact]
    public void MarketFxRateLoader_BuildFromSamples_MapsKnownMarketCodes()
    {
        MarketFxSample[] samples =
        [
            new MarketFxSample("USDRUB_TOM", new DateTime(2026, 4, 20, 0, 0, 0, DateTimeKind.Utc), 80m, "moex"),
            new MarketFxSample("UNKNOWN", new DateTime(2026, 4, 20, 0, 0, 0, DateTimeKind.Utc), 10m, "test")
        ];

        var rates = MarketFxRateLoader.BuildFromSamples(samples);

        Assert.Single(rates);
        Assert.Equal("USD", rates[0].BaseCurrencyId);
        Assert.Equal("RUB", rates[0].QuoteCurrencyId);
        Assert.Equal("MARKET_MOEX", rates[0].Source);
    }

    [Fact]
    public void ValuationService_SelectsStaticAverageAlias_AndOrdersInput()
    {
        var instrumentId = Guid.NewGuid();
        var buy = new ValuationOperation(
            instrumentId,
            OperationType.Buy,
            10m,
            100m,
            0m,
            new DateTime(2026, 4, 20, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 4, 20, 0, 0, 0, DateTimeKind.Utc));
        var sell = new ValuationOperation(
            instrumentId,
            OperationType.Sell,
            5m,
            120m,
            0m,
            new DateTime(2026, 4, 21, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 4, 21, 0, 0, 0, DateTimeKind.Utc));

        var result = new ValuationService().Evaluate([sell, buy], "staticAverage", assumeSorted: false);

        Assert.Equal(5m, result.Positions[instrumentId].Quantity);
        Assert.Equal(100m, result.Positions[instrumentId].AverageCost);
        Assert.Equal(100m, result.RealizedByInstrument[instrumentId]);
    }

    [Fact]
    public void ValuationService_DistinguishesFifoAndLifoRealizedCost()
    {
        var instrumentId = Guid.NewGuid();
        ValuationOperation[] operations =
        [
            new(
                instrumentId,
                OperationType.Buy,
                10m,
                100m,
                0m,
                new DateTime(2026, 4, 20, 0, 0, 0, DateTimeKind.Utc),
                new DateTime(2026, 4, 20, 0, 0, 0, DateTimeKind.Utc)),
            new(
                instrumentId,
                OperationType.Buy,
                10m,
                120m,
                0m,
                new DateTime(2026, 4, 21, 0, 0, 0, DateTimeKind.Utc),
                new DateTime(2026, 4, 21, 0, 0, 0, DateTimeKind.Utc)),
            new(
                instrumentId,
                OperationType.Sell,
                5m,
                130m,
                0m,
                new DateTime(2026, 4, 22, 0, 0, 0, DateTimeKind.Utc),
                new DateTime(2026, 4, 22, 0, 0, 0, DateTimeKind.Utc))
        ];

        var service = new ValuationService();

        var fifo = service.Evaluate(operations, "fifo", assumeSorted: true);
        var lifo = service.Evaluate(operations, "lifo", assumeSorted: true);

        Assert.Equal(150m, fifo.RealizedByInstrument[instrumentId]);
        Assert.Equal(50m, lifo.RealizedByInstrument[instrumentId]);
        Assert.Equal(1700m / 15m, fifo.Positions[instrumentId].AverageCost);
        Assert.Equal(1600m / 15m, lifo.Positions[instrumentId].AverageCost);
    }

    [Fact]
    public void ValuationService_KeepsTotalCostAcrossSplit_ForLotBasedStrategies()
    {
        var instrumentId = Guid.NewGuid();
        ValuationOperation[] operations =
        [
            new(
                instrumentId,
                OperationType.Buy,
                3m,
                90m,
                0m,
                new DateTime(2026, 4, 20, 0, 0, 0, DateTimeKind.Utc),
                new DateTime(2026, 4, 20, 0, 0, 0, DateTimeKind.Utc)),
            new(
                instrumentId,
                OperationType.Split,
                2m,
                0m,
                0m,
                new DateTime(2026, 4, 21, 0, 0, 0, DateTimeKind.Utc),
                new DateTime(2026, 4, 21, 0, 0, 0, DateTimeKind.Utc))
        ];

        var service = new ValuationService();

        var fifo = service.Evaluate(operations, "fifo", assumeSorted: true);
        var lifo = service.Evaluate(operations, "lifo", assumeSorted: true);

        Assert.Equal(6m, fifo.Positions[instrumentId].Quantity);
        Assert.Equal(-270m, fifo.Positions[instrumentId].RollingCost);
        Assert.Equal(45m, fifo.Positions[instrumentId].AverageCost);
        Assert.Equal(6m, lifo.Positions[instrumentId].Quantity);
        Assert.Equal(-270m, lifo.Positions[instrumentId].RollingCost);
        Assert.Equal(45m, lifo.Positions[instrumentId].AverageCost);
    }
}
