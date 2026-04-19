using Larchik.Application.Helpers;
using Larchik.Persistence.Entities;
using Xunit;

namespace Larchik.Application.Tests.Helpers;

public sealed class InstrumentCorporateActionOperationMergerTests
{
    [Fact]
    public void Merge_AddsSyntheticCorporateAction_AfterSameDayOperations()
    {
        var portfolioId = Guid.NewGuid();
        var instrumentId = Guid.NewGuid();
        var tradeDate = new DateTime(2026, 4, 20, 0, 0, 0, DateTimeKind.Utc);
        Operation[] operations =
        [
            new()
            {
                Id = Guid.NewGuid(),
                PortfolioId = portfolioId,
                InstrumentId = instrumentId,
                Type = OperationType.Buy,
                Quantity = 1m,
                Price = 90m,
                CurrencyId = "USD",
                TradeDate = tradeDate.AddDays(-1),
                SettlementDate = tradeDate.AddDays(-1),
                CreatedAt = tradeDate.AddDays(-1),
                UpdatedAt = tradeDate.AddDays(-1)
            },
            new()
            {
                Id = Guid.NewGuid(),
                PortfolioId = portfolioId,
                InstrumentId = instrumentId,
                Type = OperationType.Buy,
                Quantity = 1m,
                Price = 100m,
                CurrencyId = "USD",
                TradeDate = tradeDate,
                SettlementDate = tradeDate,
                CreatedAt = tradeDate.AddHours(10),
                UpdatedAt = tradeDate.AddHours(10)
            }
        ];
        InstrumentCorporateAction[] actions =
        [
            new()
            {
                Id = Guid.NewGuid(),
                InstrumentId = instrumentId,
                Type = OperationType.Split,
                Factor = 2m,
                EffectiveDate = tradeDate,
                Note = "2-for-1 split"
            }
        ];
        var instruments = new Dictionary<Guid, Instrument>
        {
            [instrumentId] = new()
            {
                Id = instrumentId,
                Ticker = "AAPL",
                Name = "Apple",
                Type = InstrumentType.Equity,
                CurrencyId = "USD"
            }
        };

        var merged = InstrumentCorporateActionOperationMerger.Merge(operations, actions, instruments);

        Assert.Equal(3, merged.Count);
        Assert.Equal(OperationType.Buy, merged[0].Type);
        Assert.Equal(OperationType.Buy, merged[1].Type);
        Assert.Equal(OperationType.Split, merged[2].Type);
        Assert.Equal(2m, merged[2].Quantity);
        Assert.Equal("USD", merged[2].CurrencyId);
        Assert.Equal(new DateTime(9999, 12, 31, 23, 59, 59, DateTimeKind.Utc), merged[2].CreatedAt);
    }

    [Fact]
    public void Merge_RemovesLegacyCorporateActionOperation_WhenMatchingCorporateActionExists()
    {
        var portfolioId = Guid.NewGuid();
        var instrumentId = Guid.NewGuid();
        var tradeDate = new DateTime(2026, 4, 20, 0, 0, 0, DateTimeKind.Utc);
        Operation[] operations =
        [
            new()
            {
                Id = Guid.NewGuid(),
                PortfolioId = portfolioId,
                InstrumentId = instrumentId,
                Type = OperationType.Buy,
                Quantity = 1m,
                Price = 100m,
                CurrencyId = "USD",
                TradeDate = tradeDate.AddDays(-1),
                SettlementDate = tradeDate.AddDays(-1),
                CreatedAt = tradeDate.AddDays(-1),
                UpdatedAt = tradeDate.AddDays(-1)
            },
            new()
            {
                Id = Guid.NewGuid(),
                PortfolioId = portfolioId,
                InstrumentId = instrumentId,
                Type = OperationType.Split,
                Quantity = 2m,
                Price = 0m,
                Fee = 0m,
                CurrencyId = "USD",
                TradeDate = tradeDate,
                SettlementDate = tradeDate,
                CreatedAt = tradeDate,
                UpdatedAt = tradeDate
            }
        ];
        InstrumentCorporateAction[] actions =
        [
            new()
            {
                Id = Guid.NewGuid(),
                InstrumentId = instrumentId,
                Type = OperationType.Split,
                Factor = 2m,
                EffectiveDate = tradeDate,
                Note = "2-for-1 split"
            }
        ];
        var instruments = new Dictionary<Guid, Instrument>
        {
            [instrumentId] = new()
            {
                Id = instrumentId,
                Ticker = "AAPL",
                Name = "Apple",
                Type = InstrumentType.Equity,
                CurrencyId = "USD"
            }
        };

        var merged = InstrumentCorporateActionOperationMerger.Merge(operations, actions, instruments);

        Assert.Equal(2, merged.Count);
        Assert.Equal(OperationType.Buy, merged[0].Type);
        Assert.Equal(OperationType.Split, merged[1].Type);
        Assert.Equal(actions[0].Id, merged[1].Id);
    }

    [Fact]
    public void Merge_DoesNotAddCorporateAction_WithoutEarlierInstrumentOperations()
    {
        var portfolioId = Guid.NewGuid();
        var instrumentId = Guid.NewGuid();
        var tradeDate = new DateTime(2026, 4, 20, 0, 0, 0, DateTimeKind.Utc);
        Operation[] operations =
        [
            new()
            {
                Id = Guid.NewGuid(),
                PortfolioId = portfolioId,
                InstrumentId = instrumentId,
                Type = OperationType.Buy,
                Quantity = 1m,
                Price = 100m,
                CurrencyId = "USD",
                TradeDate = tradeDate,
                SettlementDate = tradeDate,
                CreatedAt = tradeDate,
                UpdatedAt = tradeDate
            }
        ];
        InstrumentCorporateAction[] actions =
        [
            new()
            {
                Id = Guid.NewGuid(),
                InstrumentId = instrumentId,
                Type = OperationType.Split,
                Factor = 2m,
                EffectiveDate = tradeDate,
                Note = "2-for-1 split"
            }
        ];
        var instruments = new Dictionary<Guid, Instrument>
        {
            [instrumentId] = new()
            {
                Id = instrumentId,
                Ticker = "AAPL",
                Name = "Apple",
                Type = InstrumentType.Equity,
                CurrencyId = "USD"
            }
        };

        var merged = InstrumentCorporateActionOperationMerger.Merge(operations, actions, instruments);

        Assert.Single(merged);
        Assert.Equal(OperationType.Buy, merged[0].Type);
    }
}
