using Larchik.Application.Portfolios.DailyAttribution;
using Larchik.Application.Portfolios.Valuation;
using Larchik.Persistence.Entities;
using Xunit;

namespace Larchik.Application.Tests.Portfolios;

public sealed class DailyPnlAttributionCalculatorTests
{
    private static readonly DateTime Start = Utc(2026, 8, 10);
    private static readonly DateTime End = Utc(2026, 8, 11);

    [Fact]
    public void Calculate_SplitsForeignSecurityMoveIntoPriceFxAndCrossEffects()
    {
        var instrumentId = Guid.NewGuid();
        var portfolio = Portfolio();
        var instrument = Instrument(instrumentId, "USD bond", InstrumentType.Bond, "USD");
        var operations = new[]
        {
            Operation(portfolio.Id, OperationType.TransferIn, Utc(2026, 8, 1), instrumentId, quantity: 10m)
        };
        var data = Data(
            prices:
            [
                Price(instrumentId, Start, 100m, "USD"),
                Price(instrumentId, End, 90m, "USD")
            ],
            fxRates:
            [
                Fx(Start, "USD", "RUB", 90m),
                Fx(End, "USD", "RUB", 95m)
            ]);

        var result = new DailyPnlAttributionCalculator().Calculate(
            portfolio,
            operations,
            new Dictionary<Guid, Instrument> { [instrumentId] = instrument },
            data,
            "RUB",
            Start,
            End);

        var row = Assert.Single(result.Positions);
        Assert.Equal(-9_000m, row.PriceEffectBase);
        Assert.Equal(5_000m, row.FxEffectBase);
        Assert.Equal(-500m, row.CrossEffectBase);
        Assert.Equal(-4_500m, row.PnlBase);
        Assert.Equal(-4_500m, result.PnlBase);
        Assert.Equal(0m, result.OtherEffectBase);
        Assert.Equal(-0.10m, row.PriceReturnPct);
        Assert.Equal(95m / 90m - 1m, row.FxReturnPct);
    }

    [Fact]
    public void Calculate_AttributesForeignCashMoveToCashFx()
    {
        var portfolio = Portfolio();
        var operations = new[]
        {
            Operation(portfolio.Id, OperationType.Deposit, Utc(2026, 8, 1), price: 1_000m, currency: "USD")
        };
        var data = Data(
            fxRates:
            [
                Fx(Start, "USD", "RUB", 90m),
                Fx(End, "USD", "RUB", 88m)
            ]);

        var result = new DailyPnlAttributionCalculator().Calculate(
            portfolio,
            operations,
            new Dictionary<Guid, Instrument>(),
            data,
            "RUB",
            Start,
            End);

        Assert.Equal(-2_000m, result.CashFxEffectBase);
        Assert.Equal(-2_000m, result.PnlBase);
        Assert.Equal(0m, result.OtherEffectBase);
    }

    [Fact]
    public void Calculate_SeparatesSameDayTradeGainAndFee()
    {
        var instrumentId = Guid.NewGuid();
        var portfolio = Portfolio();
        var instrument = Instrument(instrumentId, "Share", InstrumentType.Equity, "RUB");
        var operations = new[]
        {
            Operation(portfolio.Id, OperationType.Deposit, Utc(2026, 8, 1), price: 10_000m),
            Operation(portfolio.Id, OperationType.Buy, End, instrumentId, quantity: 10m, price: 100m, fee: 10m)
        };
        var data = Data(prices: [Price(instrumentId, End, 110m, "RUB")]);

        var result = new DailyPnlAttributionCalculator().Calculate(
            portfolio,
            operations,
            new Dictionary<Guid, Instrument> { [instrumentId] = instrument },
            data,
            "RUB",
            Start,
            End);

        var row = Assert.Single(result.Positions);
        Assert.Equal(100m, row.TradingEffectBase);
        Assert.Equal(-10m, row.FeeEffectBase);
        Assert.Equal(90m, result.PnlBase);
        Assert.Equal(0m, result.OtherEffectBase);
    }

    [Fact]
    public void Calculate_TreatsCouponAsIncomeInsteadOfHidingExCouponPriceDrop()
    {
        var instrumentId = Guid.NewGuid();
        var portfolio = Portfolio();
        var instrument = Instrument(instrumentId, "Bond", InstrumentType.Bond, "RUB");
        var operations = new[]
        {
            Operation(portfolio.Id, OperationType.TransferIn, Utc(2026, 8, 1), instrumentId, quantity: 10m),
            Operation(portfolio.Id, OperationType.Dividend, End, instrumentId, price: 20m)
        };
        var data = Data(prices:
        [
            Price(instrumentId, Start, 100m, "RUB"),
            Price(instrumentId, End, 98m, "RUB")
        ]);

        var result = new DailyPnlAttributionCalculator().Calculate(
            portfolio,
            operations,
            new Dictionary<Guid, Instrument> { [instrumentId] = instrument },
            data,
            "RUB",
            Start,
            End);

        var row = Assert.Single(result.Positions);
        Assert.Equal(-20m, row.PriceEffectBase);
        Assert.Equal(20m, row.IncomeEffectBase);
        Assert.Equal(0m, row.PnlBase);
        Assert.Equal(0m, result.PnlBase);
    }

    [Fact]
    public void Calculate_ExcludesSecurityTransferAtFairValueFromDailyPnl()
    {
        var instrumentId = Guid.NewGuid();
        var portfolio = Portfolio();
        var instrument = Instrument(instrumentId, "Share", InstrumentType.Equity, "RUB");
        var operations = new[]
        {
            Operation(portfolio.Id, OperationType.TransferIn, End, instrumentId, quantity: 10m)
        };
        var data = Data(prices:
        [
            Price(instrumentId, Start, 100m, "RUB"),
            Price(instrumentId, End, 100m, "RUB")
        ]);

        var result = new DailyPnlAttributionCalculator().Calculate(
            portfolio,
            operations,
            new Dictionary<Guid, Instrument> { [instrumentId] = instrument },
            data,
            "RUB",
            Start,
            End);

        Assert.Equal(1_000m, result.ExternalFlowBase);
        Assert.Equal(0m, result.PnlBase);
        Assert.Equal(0m, Assert.Single(result.Positions).PnlBase);
    }

    private static Portfolio Portfolio() => new()
    {
        Id = Guid.NewGuid(),
        Name = "Test",
        ReportingCurrencyId = "RUB"
    };

    private static Instrument Instrument(Guid id, string name, InstrumentType type, string currency) => new()
    {
        Id = id,
        Name = name,
        Ticker = name,
        Type = type,
        CurrencyId = currency
    };

    private static Operation Operation(
        Guid portfolioId,
        OperationType type,
        DateTime date,
        Guid? instrumentId = null,
        decimal quantity = 0m,
        decimal price = 0m,
        decimal fee = 0m,
        string currency = "RUB") => new()
    {
        Id = Guid.NewGuid(),
        PortfolioId = portfolioId,
        InstrumentId = instrumentId,
        Type = type,
        Quantity = quantity,
        Price = price,
        Fee = fee,
        CurrencyId = currency,
        TradeDate = date,
        CreatedAt = date,
        UpdatedAt = date
    };

    private static Price Price(Guid instrumentId, DateTime date, decimal value, string currency) => new()
    {
        Id = Guid.NewGuid(),
        InstrumentId = instrumentId,
        Date = date,
        Value = value,
        CurrencyId = currency,
        Provider = "TEST",
        CreatedAt = date,
        UpdatedAt = date
    };

    private static FxRate Fx(DateTime date, string from, string to, decimal rate) => new()
    {
        Id = Guid.NewGuid(),
        Date = date,
        BaseCurrencyId = from,
        QuoteCurrencyId = to,
        Rate = rate,
        Source = "TEST",
        CreatedAt = date,
        UpdatedAt = date
    };

    private static HistoricalDataLookup Data(
        IReadOnlyCollection<Price>? prices = null,
        IReadOnlyCollection<FxRate>? fxRates = null) => new(prices ?? [], fxRates ?? []);

    private static DateTime Utc(int year, int month, int day) =>
        new(year, month, day, 0, 0, 0, DateTimeKind.Utc);
}
