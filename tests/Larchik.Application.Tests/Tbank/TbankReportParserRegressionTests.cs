using Larchik.Persistence.Entities;
using Xunit;

namespace Larchik.Application.Tests.Tbank;

public class TbankReportParserRegressionTests
{
    public static IEnumerable<object[]> FixtureFiles() => TbankReportFixtureHelper.FixtureFiles();

    public static IEnumerable<object[]> FullYearBreakdowns()
    {
        yield return
        [
            "broker-report-2019-11-01-2019-12-31.xlsx",
            new[]
            {
                new OperationBreakdown(OperationType.Buy, 2),
                new OperationBreakdown(OperationType.Fee, 2),
                new OperationBreakdown(OperationType.Deposit, 2),
                new OperationBreakdown(OperationType.CashAdjustment, 1)
            }
        ];

        yield return
        [
            "broker-report-2020-01-01-2020-12-31.xlsx",
            new[]
            {
                new OperationBreakdown(OperationType.Buy, 5),
                new OperationBreakdown(OperationType.Dividend, 1),
                new OperationBreakdown(OperationType.Fee, 2),
                new OperationBreakdown(OperationType.Deposit, 5),
                new OperationBreakdown(OperationType.CashAdjustment, 5)
            }
        ];

        yield return
        [
            "broker-report-2021-01-01-2021-12-31.xlsx",
            new[]
            {
                new OperationBreakdown(OperationType.Buy, 61),
                new OperationBreakdown(OperationType.Sell, 2),
                new OperationBreakdown(OperationType.Dividend, 7),
                new OperationBreakdown(OperationType.Fee, 13),
                new OperationBreakdown(OperationType.Deposit, 24),
                new OperationBreakdown(OperationType.Withdraw, 2),
                new OperationBreakdown(OperationType.CashAdjustment, 16)
            }
        ];

        yield return
        [
            "broker-report-2022-01-01-2022-12-31.xlsx",
            new[]
            {
                new OperationBreakdown(OperationType.Buy, 73),
                new OperationBreakdown(OperationType.Sell, 13),
                new OperationBreakdown(OperationType.Dividend, 4),
                new OperationBreakdown(OperationType.Fee, 51),
                new OperationBreakdown(OperationType.Deposit, 28),
                new OperationBreakdown(OperationType.Withdraw, 11),
                new OperationBreakdown(OperationType.CashAdjustment, 71)
            }
        ];

        yield return
        [
            "broker-report-2023-01-01-2023-12-31.xlsx",
            new[]
            {
                new OperationBreakdown(OperationType.Buy, 63),
                new OperationBreakdown(OperationType.Sell, 8),
                new OperationBreakdown(OperationType.Dividend, 58),
                new OperationBreakdown(OperationType.Fee, 73),
                new OperationBreakdown(OperationType.Deposit, 40),
                new OperationBreakdown(OperationType.BondPartialRedemption, 2),
                new OperationBreakdown(OperationType.CashAdjustment, 49)
            }
        ];

        yield return
        [
            "broker-report-2024-01-01-2024-12-31.xlsx",
            new[]
            {
                new OperationBreakdown(OperationType.Buy, 79),
                new OperationBreakdown(OperationType.Sell, 123),
                new OperationBreakdown(OperationType.Dividend, 230),
                new OperationBreakdown(OperationType.Fee, 119),
                new OperationBreakdown(OperationType.Deposit, 88),
                new OperationBreakdown(OperationType.Withdraw, 19),
                new OperationBreakdown(OperationType.BondPartialRedemption, 15),
                new OperationBreakdown(OperationType.BondMaturity, 1),
                new OperationBreakdown(OperationType.CashAdjustment, 71)
            }
        ];

        yield return
        [
            "broker-report-2025-01-01-2025-12-31.xlsx",
            new[]
            {
                new OperationBreakdown(OperationType.Buy, 166),
                new OperationBreakdown(OperationType.Sell, 11),
                new OperationBreakdown(OperationType.Dividend, 216),
                new OperationBreakdown(OperationType.Fee, 90),
                new OperationBreakdown(OperationType.Deposit, 83),
                new OperationBreakdown(OperationType.BondPartialRedemption, 11),
                new OperationBreakdown(OperationType.BondMaturity, 1),
                new OperationBreakdown(OperationType.CashAdjustment, 92)
            }
        ];

        yield return
        [
            "broker-report-2026-01-01-2026-03-17.xlsx",
            new[]
            {
                new OperationBreakdown(OperationType.Buy, 104),
                new OperationBreakdown(OperationType.Sell, 8),
                new OperationBreakdown(OperationType.Dividend, 87),
                new OperationBreakdown(OperationType.Fee, 31),
                new OperationBreakdown(OperationType.Deposit, 15),
                new OperationBreakdown(OperationType.BondPartialRedemption, 1),
                new OperationBreakdown(OperationType.CashAdjustment, 30)
            }
        ];
    }

    [Theory]
    [MemberData(nameof(FixtureFiles))]
    public void Parse_AllFixtures_WithoutErrors(string fileName)
    {
        var result = TbankReportFixtureHelper.Parse(fileName);

        Assert.Empty(result.Errors);
        Assert.NotEmpty(result.Operations);
    }

    [Theory]
    [MemberData(nameof(FullYearBreakdowns))]
    public void Parse_FullYearReports_HaveExpectedBreakdown(string fileName, OperationBreakdown[] expectedBreakdown)
    {
        var result = TbankReportFixtureHelper.Parse(fileName);
        var actual = result.Operations
            .GroupBy(x => x.Operation.Type)
            .ToDictionary(x => x.Key, x => x.Count());

        Assert.Equal(expectedBreakdown.Sum(x => x.Count), result.Operations.Count);

        foreach (var expected in expectedBreakdown)
        {
            Assert.True(actual.TryGetValue(expected.Type, out var actualCount),
                $"Expected operation type '{expected.Type}' was not parsed from {fileName}.");
            Assert.Equal(expected.Count, actualCount);
        }

        var unexpectedTypes = actual.Keys.Except(expectedBreakdown.Select(x => x.Type)).ToArray();
        Assert.True(unexpectedTypes.Length == 0,
            $"Unexpected operation types in {fileName}: {string.Join(", ", unexpectedTypes)}");
    }

    [Fact]
    public void Parse_2024_Report_ParsesBlockedForeignSellsInRub()
    {
        var result = TbankReportFixtureHelper.Parse("broker-report-2024-01-01-2024-12-31.xlsx");
        var sells = result.Operations
            .Where(x => x.Operation.Type == OperationType.Sell)
            .Select(x => new TradeSnapshot(
                x.InstrumentCode,
                x.Operation.TradeDate.Date,
                x.Operation.Quantity,
                x.Operation.Price,
                x.Operation.CurrencyId))
            .ToArray();

        Assert.Contains(new TradeSnapshot("US1104481072", new DateTime(2024, 8, 12), 2m, 2757.58m, "RUB"), sells);
        Assert.Contains(new TradeSnapshot("US1104481072", new DateTime(2024, 10, 11), 1m, 2895.46m, "RUB"), sells);
        Assert.Contains(new TradeSnapshot("US4404521001", new DateTime(2024, 8, 12), 1m, 3161.24m, "RUB"), sells);
        Assert.Contains(new TradeSnapshot("US4404521001", new DateTime(2024, 10, 11), 1m, 3319.30m, "RUB"), sells);
        Assert.Contains(new TradeSnapshot("US91912E1055", new DateTime(2024, 8, 12), 1m, 1119.95m, "RUB"), sells);
    }

    [Fact]
    public void Parse_2025_Report_ParsesBondMaturityForRu000A104Tm1()
    {
        var result = TbankReportFixtureHelper.Parse("broker-report-2025-01-01-2025-12-31.xlsx");

        var maturity = Assert.Single(result.Operations, x =>
            x.Operation.Type == OperationType.BondMaturity &&
            string.Equals(x.InstrumentCode, "RU000A104TM1", StringComparison.OrdinalIgnoreCase));

        Assert.Equal(new DateTime(2025, 5, 15), maturity.Operation.TradeDate.Date);
        Assert.Equal(1m, maturity.Operation.Quantity);
        Assert.Equal(1000m, maturity.Operation.Price);
        Assert.Equal("RUB", maturity.Operation.CurrencyId);
    }

    [Fact]
    public void Parse_2026_Report_ParsesInterleasingPartialRedemption()
    {
        var result = TbankReportFixtureHelper.Parse("broker-report-2026-01-01-2026-03-17.xlsx");

        var redemption = Assert.Single(result.Operations, x =>
            x.Operation.Type == OperationType.BondPartialRedemption &&
            string.Equals(x.InstrumentCode, "RU000A10B4A4", StringComparison.OrdinalIgnoreCase));

        Assert.Equal(new DateTime(2026, 3, 17), redemption.Operation.TradeDate.Date);
        Assert.Equal(9m, redemption.Operation.Quantity);
        Assert.Equal(27m, redemption.Operation.Price);
        Assert.Equal("RUB", redemption.Operation.CurrencyId);
    }

    [Fact]
    public void Parse_2019_Report_UsesClientWithheldCommissionFromBrokerColumn()
    {
        var result = TbankReportFixtureHelper.Parse("broker-report-2019-11-01-2019-12-31.xlsx");

        var buys = result.Operations.Where(x =>
            x.Operation.Type == OperationType.Buy &&
            string.Equals(x.InstrumentCode, "RU000A0JP5V6", StringComparison.OrdinalIgnoreCase) &&
            x.Operation.TradeDate.Date == new DateTime(2019, 11, 19) &&
            x.Operation.Quantity == 10000m).ToArray();

        Assert.Equal(2, buys.Length);
        Assert.All(buys, buy =>
        {
            Assert.Equal(1.4m, buy.Operation.Fee);
            Assert.Equal("RUB", buy.Operation.CurrencyId);
        });
    }

    [Fact]
    public void Parse_2026_03_24_Report_UsesOnlyClientWithheldCommissionForMtsPlacement()
    {
        var result = TbankReportFixtureHelper.Parse("broker-report-2026-01-01-2026-03-24.xlsx");

        var placement = Assert.Single(result.Operations, x =>
            x.Operation.Type == OperationType.Buy &&
            string.Equals(x.InstrumentCode, "RU000A10ELF6", StringComparison.OrdinalIgnoreCase) &&
            x.Operation.TradeDate.Date == new DateTime(2026, 3, 18));

        Assert.Equal(10m, placement.Operation.Quantity);
        Assert.Equal(1000m, placement.Operation.Price);
        Assert.Equal(0m, placement.Operation.Fee);
        Assert.Equal("RUB", placement.Operation.CurrencyId);
    }

    public sealed record OperationBreakdown(OperationType Type, int Count);

    private sealed record TradeSnapshot(
        string? InstrumentCode,
        DateTime TradeDate,
        decimal Quantity,
        decimal Price,
        string CurrencyId);
}
