using Larchik.Application.Operations.ImportBroker;
using Larchik.Application.Helpers;
using Larchik.Application.Portfolios.Valuation;
using Larchik.Persistence.Entities;
using Xunit;

namespace Larchik.Application.Tests.Portfolios;

public sealed class ValuationHelpersTests
{
    private static readonly string[] AllValuationMethods = ["adjustingAvg", "staticAvg", "fifo", "lifo"];

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
    public void HistoricalDataLookup_DoesNotUseFutureFxRates()
    {
        var rateDate = new DateTime(2026, 4, 20, 0, 0, 0, DateTimeKind.Utc);
        var asOfDate = rateDate.AddDays(-1);
        FxRate[] fxRates =
        [
            new FxRate
            {
                BaseCurrencyId = "USD",
                QuoteCurrencyId = "RUB",
                Date = rateDate,
                Rate = 80m,
                Source = "CBR",
                CreatedAt = rateDate
            }
        ];

        var lookup = new HistoricalDataLookup([], fxRates);

        Assert.Null(lookup.GetRate("USD", "RUB", asOfDate));
    }

    [Fact]
    public void HistoricalDataLookup_UsesCrossRate_WhenDirectPairIsMissing()
    {
        var date = new DateTime(2026, 4, 20, 0, 0, 0, DateTimeKind.Utc);
        FxRate[] fxRates =
        [
            new()
            {
                BaseCurrencyId = "USD",
                QuoteCurrencyId = "RUB",
                Date = date,
                Rate = 80m,
                Source = "CBR",
                CreatedAt = date
            },
            new()
            {
                BaseCurrencyId = "EUR",
                QuoteCurrencyId = "RUB",
                Date = date,
                Rate = 100m,
                Source = "CBR",
                CreatedAt = date
            }
        ];

        var lookup = new HistoricalDataLookup([], fxRates);

        var usdToEur = lookup.GetRate("USD", "EUR", date);

        Assert.Equal(0.8m, usdToEur);
        Assert.Equal(8m, lookup.Convert(10m, "USD", "EUR", date));
    }

    [Fact]
    public void HistoricalDataLookup_PrefersRubleRoute_WhenMultipleCrossRoutesExist()
    {
        var date = new DateTime(2026, 4, 20, 0, 0, 0, DateTimeKind.Utc);
        FxRate[] fxRates =
        [
            new()
            {
                BaseCurrencyId = "USD",
                QuoteCurrencyId = "RUB",
                Date = date,
                Rate = 80m,
                Source = "CBR",
                CreatedAt = date
            },
            new()
            {
                BaseCurrencyId = "EUR",
                QuoteCurrencyId = "RUB",
                Date = date,
                Rate = 100m,
                Source = "CBR",
                CreatedAt = date
            },
            new()
            {
                BaseCurrencyId = "USD",
                QuoteCurrencyId = "KZT",
                Date = date,
                Rate = 500m,
                Source = "CBR",
                CreatedAt = date
            },
            new()
            {
                BaseCurrencyId = "EUR",
                QuoteCurrencyId = "KZT",
                Date = date,
                Rate = 700m,
                Source = "CBR",
                CreatedAt = date
            }
        ];

        var lookup = new HistoricalDataLookup([], fxRates);

        // RUB route yields 0.8, KZT route yields 500/700 ~= 0.714285.
        var usdToEur = lookup.GetRate("USD", "EUR", date);

        Assert.Equal(0.8m, usdToEur);
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

    [Theory]
    [InlineData("fifo")]
    [InlineData("lifo")]
    public void LotValuation_ClearsCostAfterFullLiquidationBeforeTransferIn(string method)
    {
        var instrumentId = Guid.NewGuid();
        ValuationOperation[] operations =
        [
            new(
                instrumentId,
                OperationType.Buy,
                1m,
                100m,
                1m,
                new DateTime(2026, 4, 20, 0, 0, 0, DateTimeKind.Utc),
                new DateTime(2026, 4, 20, 0, 0, 0, DateTimeKind.Utc)),
            new(
                instrumentId,
                OperationType.Sell,
                1m,
                110m,
                2m,
                new DateTime(2026, 4, 21, 0, 0, 0, DateTimeKind.Utc),
                new DateTime(2026, 4, 21, 0, 0, 0, DateTimeKind.Utc)),
            new(
                instrumentId,
                OperationType.TransferIn,
                1m,
                0m,
                0m,
                new DateTime(2026, 4, 22, 0, 0, 0, DateTimeKind.Utc),
                new DateTime(2026, 4, 22, 0, 0, 0, DateTimeKind.Utc))
        ];

        var result = new ValuationService().Evaluate(operations, method, assumeSorted: true);

        Assert.Equal(1m, result.Positions[instrumentId].Quantity);
        Assert.Equal(0m, result.Positions[instrumentId].AverageCost);
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

    [Theory]
    [InlineData("adjustingAvg")]
    [InlineData("staticAvg")]
    [InlineData("fifo")]
    [InlineData("lifo")]
    public void ValuationService_ReverseSplitPreservesFractionalQuantity_WithoutRounding(string method)
    {
        var instrumentId = Guid.NewGuid();
        ValuationOperation[] operations =
        [
            new(
                instrumentId,
                OperationType.Buy,
                1m,
                100m,
                0m,
                new DateTime(2026, 4, 20, 0, 0, 0, DateTimeKind.Utc),
                new DateTime(2026, 4, 20, 0, 0, 0, DateTimeKind.Utc)),
            new(
                instrumentId,
                OperationType.ReverseSplit,
                0.5m,
                0m,
                0m,
                new DateTime(2026, 4, 21, 0, 0, 0, DateTimeKind.Utc),
                CorporateActionOperationMetadata.SyntheticCreatedAt)
        ];

        var result = new ValuationService().Evaluate(operations, method, assumeSorted: true);
        var position = result.Positions[instrumentId];

        Assert.Equal(0.5m, position.Quantity);
        Assert.Equal(-100m, position.RollingCost);
        Assert.Equal(200m, position.AverageCost);
        Assert.Empty(result.RealizedByInstrument);
    }

    [Theory]
    [InlineData("adjustingAvg")]
    [InlineData("staticAvg")]
    [InlineData("fifo")]
    [InlineData("lifo")]
    public void ValuationService_LegacyReverseSplitOperation_RoundsForBrokerCompatibility(string method)
    {
        var instrumentId = Guid.NewGuid();
        var legacyCreatedAt = new DateTime(2026, 4, 21, 0, 0, 0, DateTimeKind.Utc);
        ValuationOperation[] operations =
        [
            new(
                instrumentId,
                OperationType.Buy,
                1m,
                100m,
                0m,
                new DateTime(2026, 4, 20, 0, 0, 0, DateTimeKind.Utc),
                new DateTime(2026, 4, 20, 0, 0, 0, DateTimeKind.Utc)),
            new(
                instrumentId,
                OperationType.ReverseSplit,
                0.5m,
                0m,
                0m,
                legacyCreatedAt,
                legacyCreatedAt)
        ];

        var result = new ValuationService().Evaluate(operations, method, assumeSorted: true);
        var position = result.Positions[instrumentId];

        Assert.Equal(1m, position.Quantity);
        Assert.Equal(-100m, position.RollingCost);
        Assert.Equal(100m, position.AverageCost);
    }

    [Theory]
    [InlineData("adjustingAvg")]
    [InlineData("staticAvg")]
    [InlineData("fifo")]
    [InlineData("lifo")]
    public void ValuationService_TreatsSecurityTransferIn_AsZeroCostQuantity_WithoutRealizedPnl(string method)
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
                OperationType.TransferIn,
                10m,
                0m,
                0m,
                new DateTime(2026, 4, 21, 0, 0, 0, DateTimeKind.Utc),
                new DateTime(2026, 4, 21, 0, 0, 0, DateTimeKind.Utc))
        ];

        var result = new ValuationService().Evaluate(operations, method, assumeSorted: true);

        Assert.Equal(20m, result.Positions[instrumentId].Quantity);
        Assert.Equal(-1000m, result.Positions[instrumentId].RollingCost);
        Assert.Equal(50m, result.Positions[instrumentId].AverageCost);
        Assert.False(result.RealizedByInstrument.ContainsKey(instrumentId));
    }

    [Theory]
    [InlineData("adjustingAvg")]
    [InlineData("staticAvg")]
    [InlineData("fifo")]
    [InlineData("lifo")]
    public void ValuationService_TreatsSecurityTransferOut_AsQuantityReduction_WithoutRealizedPnl(string method)
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
                OperationType.TransferOut,
                5m,
                0m,
                0m,
                new DateTime(2026, 4, 21, 0, 0, 0, DateTimeKind.Utc),
                new DateTime(2026, 4, 21, 0, 0, 0, DateTimeKind.Utc))
        ];

        var result = new ValuationService().Evaluate(operations, method, assumeSorted: true);

        Assert.Equal(5m, result.Positions[instrumentId].Quantity);
        Assert.Equal(-1000m, result.Positions[instrumentId].RollingCost);
        Assert.Equal(200m, result.Positions[instrumentId].AverageCost);
        Assert.False(result.RealizedByInstrument.ContainsKey(instrumentId));
    }

    [Fact]
    public void ValuationService_Fifo_TransferOut_MultiLot_DefinesLaterSellPnl()
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
                OperationType.TransferOut,
                5m,
                0m,
                0m,
                new DateTime(2026, 4, 22, 0, 0, 0, DateTimeKind.Utc),
                new DateTime(2026, 4, 22, 0, 0, 0, DateTimeKind.Utc)),
            new(
                instrumentId,
                OperationType.Sell,
                5m,
                130m,
                0m,
                new DateTime(2026, 4, 23, 0, 0, 0, DateTimeKind.Utc),
                new DateTime(2026, 4, 23, 0, 0, 0, DateTimeKind.Utc))
        ];

        var result = new ValuationService().Evaluate(operations, "fifo", assumeSorted: true);

        Assert.Equal(10m, result.Positions[instrumentId].Quantity);
        Assert.Equal(-1200m, result.Positions[instrumentId].RollingCost);
        Assert.Equal(-350m, result.RealizedByInstrument[instrumentId]);
    }

    [Fact]
    public void ValuationService_Lifo_TransferOut_MultiLot_DefinesLaterSellPnl()
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
                OperationType.TransferOut,
                5m,
                0m,
                0m,
                new DateTime(2026, 4, 22, 0, 0, 0, DateTimeKind.Utc),
                new DateTime(2026, 4, 22, 0, 0, 0, DateTimeKind.Utc)),
            new(
                instrumentId,
                OperationType.Sell,
                5m,
                130m,
                0m,
                new DateTime(2026, 4, 23, 0, 0, 0, DateTimeKind.Utc),
                new DateTime(2026, 4, 23, 0, 0, 0, DateTimeKind.Utc))
        ];

        var result = new ValuationService().Evaluate(operations, "lifo", assumeSorted: true);

        Assert.Equal(10m, result.Positions[instrumentId].Quantity);
        Assert.Equal(-1000m, result.Positions[instrumentId].RollingCost);
        Assert.Equal(-550m, result.RealizedByInstrument[instrumentId]);
    }

    [Theory]
    [InlineData("fifo")]
    [InlineData("lifo")]
    public void ValuationService_TransferOut_FullLot_KeepsCostInRemainingLots_ForLaterSell(string method)
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
                OperationType.TransferOut,
                10m,
                0m,
                0m,
                new DateTime(2026, 4, 22, 0, 0, 0, DateTimeKind.Utc),
                new DateTime(2026, 4, 22, 0, 0, 0, DateTimeKind.Utc)),
            new(
                instrumentId,
                OperationType.Sell,
                5m,
                130m,
                0m,
                new DateTime(2026, 4, 23, 0, 0, 0, DateTimeKind.Utc),
                new DateTime(2026, 4, 23, 0, 0, 0, DateTimeKind.Utc))
        ];

        var result = new ValuationService().Evaluate(operations, method, assumeSorted: true);

        Assert.Equal(5m, result.Positions[instrumentId].Quantity);
        Assert.Equal(-1100m, result.Positions[instrumentId].RollingCost);
        Assert.Equal(-450m, result.RealizedByInstrument[instrumentId]);
    }

    [Theory]
    [InlineData("adjustingAvg")]
    [InlineData("staticAvg")]
    [InlineData("fifo")]
    [InlineData("lifo")]
    public void ValuationService_Throws_WhenDispositionExceedsAvailableQuantity(string method)
    {
        var instrumentId = Guid.NewGuid();
        ValuationOperation[] operations =
        [
            new(
                instrumentId,
                OperationType.Buy,
                5m,
                100m,
                0m,
                new DateTime(2026, 4, 20, 0, 0, 0, DateTimeKind.Utc),
                new DateTime(2026, 4, 20, 0, 0, 0, DateTimeKind.Utc)),
            new(
                instrumentId,
                OperationType.Sell,
                6m,
                110m,
                0m,
                new DateTime(2026, 4, 21, 0, 0, 0, DateTimeKind.Utc),
                new DateTime(2026, 4, 21, 0, 0, 0, DateTimeKind.Utc))
        ];

        Assert.Throws<InvalidOperationException>(() => new ValuationService().Evaluate(operations, method, assumeSorted: true));
    }

    [Theory]
    [InlineData("adjustingAvg")]
    [InlineData("staticAvg")]
    [InlineData("fifo")]
    [InlineData("lifo")]
    public void ValuationService_Throws_WhenTransferOutExceedsAvailableQuantity(string method)
    {
        var instrumentId = Guid.NewGuid();
        ValuationOperation[] operations =
        [
            new(
                instrumentId,
                OperationType.Buy,
                5m,
                100m,
                0m,
                new DateTime(2026, 4, 20, 0, 0, 0, DateTimeKind.Utc),
                new DateTime(2026, 4, 20, 0, 0, 0, DateTimeKind.Utc)),
            new(
                instrumentId,
                OperationType.TransferOut,
                6m,
                0m,
                0m,
                new DateTime(2026, 4, 21, 0, 0, 0, DateTimeKind.Utc),
                new DateTime(2026, 4, 21, 0, 0, 0, DateTimeKind.Utc))
        ];

        Assert.Throws<InvalidOperationException>(() => new ValuationService().Evaluate(operations, method, assumeSorted: true));
    }

    [Theory]
    [InlineData("fifo")]
    [InlineData("lifo")]
    public void ValuationService_Throws_WhenTransferOutFromEmptyPosition_ForLotMethods(string method)
    {
        var instrumentId = Guid.NewGuid();
        ValuationOperation[] operations =
        [
            new(
                instrumentId,
                OperationType.TransferOut,
                1m,
                0m,
                0m,
                new DateTime(2026, 4, 20, 0, 0, 0, DateTimeKind.Utc),
                new DateTime(2026, 4, 20, 0, 0, 0, DateTimeKind.Utc))
        ];

        Assert.Throws<InvalidOperationException>(() => new ValuationService().Evaluate(operations, method, assumeSorted: true));
    }

    [Fact]
    public void ValuationService_StaticAvg_ClearsCostAfterFullTransferOut()
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
                OperationType.TransferOut,
                10m,
                0m,
                0m,
                new DateTime(2026, 4, 21, 0, 0, 0, DateTimeKind.Utc),
                new DateTime(2026, 4, 21, 0, 0, 0, DateTimeKind.Utc)),
            new(
                instrumentId,
                OperationType.TransferIn,
                5m,
                0m,
                0m,
                new DateTime(2026, 4, 22, 0, 0, 0, DateTimeKind.Utc),
                new DateTime(2026, 4, 22, 0, 0, 0, DateTimeKind.Utc))
        ];

        var result = new ValuationService().Evaluate(operations, "staticAvg", assumeSorted: true);

        Assert.Equal(5m, result.Positions[instrumentId].Quantity);
        Assert.Equal(0m, result.Positions[instrumentId].RollingCost);
        Assert.Equal(0m, result.Positions[instrumentId].AverageCost);
    }

    public static IEnumerable<object[]> ValuationInvariantCases()
    {
        var instrumentId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var d1 = new DateTime(2026, 4, 20, 0, 0, 0, DateTimeKind.Utc);
        var d2 = d1.AddDays(1);
        var d3 = d1.AddDays(2);
        var d4 = d1.AddDays(3);
        var d5 = d1.AddDays(4);

        var scenarios = new (string Name, bool ExpectNoRealized, ValuationOperation[] Ops)[]
        {
            (
                "buy_split_reverseSplit_transfer_mix",
                true,
                [
                    Op(instrumentId, OperationType.Buy, 10m, 100m, 1m, d1),
                    Op(instrumentId, OperationType.Split, 2m, 0m, 0m, d2),
                    Op(instrumentId, OperationType.ReverseSplit, 0.5m, 0m, 0m, d3),
                    Op(instrumentId, OperationType.TransferOut, 3m, 0m, 0m, d4),
                    Op(instrumentId, OperationType.TransferIn, 2m, 0m, 0m, d5)
                ]
            ),
            (
                "bond_partial_redemption_then_transfer",
                true,
                [
                    Op(instrumentId, OperationType.Buy, 8m, 100m, 0m, d1),
                    Op(instrumentId, OperationType.BondPartialRedemption, 8m, 5m, 1m, d2),
                    Op(instrumentId, OperationType.TransferOut, 2m, 0m, 0m, d3),
                    Op(instrumentId, OperationType.TransferIn, 1m, 0m, 0m, d4)
                ]
            ),
            (
                "sell_and_transfer_sequence",
                false,
                [
                    Op(instrumentId, OperationType.Buy, 10m, 90m, 0m, d1),
                    Op(instrumentId, OperationType.Buy, 5m, 120m, 0m, d2),
                    Op(instrumentId, OperationType.Sell, 4m, 130m, 2m, d3),
                    Op(instrumentId, OperationType.TransferOut, 3m, 0m, 0m, d4),
                    Op(instrumentId, OperationType.TransferIn, 2m, 0m, 0m, d5)
                ]
            )
        };

        foreach (var (name, expectNoRealized, ops) in scenarios)
        {
            foreach (var method in AllValuationMethods)
            {
                yield return [name, method, expectNoRealized, ops];
            }
        }
    }

    [Theory]
    [MemberData(nameof(ValuationInvariantCases))]
    public void ValuationService_PreservesCoreInvariants_InTableDrivenScenarios(
        string scenarioName,
        string method,
        bool expectNoRealized,
        ValuationOperation[] operations)
    {
        var result = new ValuationService().Evaluate(operations, method, assumeSorted: true);

        foreach (var position in result.Positions.Values)
        {
            Assert.True(position.Quantity >= 0, $"{scenarioName}:{method} produced negative quantity.");
            if (position.Quantity == 0)
            {
                Assert.Equal(0m, position.RollingCost);
                Assert.Equal(0m, position.AverageCost);
                continue;
            }

            Assert.Equal(position.RollingCost / position.Quantity, -position.AverageCost);
        }

        if (expectNoRealized)
        {
            Assert.Empty(result.RealizedByInstrument);
        }
    }

    private static ValuationOperation Op(
        Guid instrumentId,
        OperationType type,
        decimal quantity,
        decimal price,
        decimal fee,
        DateTime date) =>
        new(
            instrumentId,
            type,
            quantity,
            price,
            fee,
            date,
            date);
}
