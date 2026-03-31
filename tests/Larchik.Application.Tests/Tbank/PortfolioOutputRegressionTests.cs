using Larchik.Application.Models;
using Larchik.Persistence.Entities;
using Xunit;

namespace Larchik.Application.Tests.Tbank;

public class PortfolioOutputRegressionTests
{
    private const string LatestImportedReportFileName = "broker-report-2026-01-01-2026-03-24.xlsx";
    private static readonly IReadOnlyList<TbankExpectedStepStates> Steps = TbankExpectedStepStates.LoadAll();
    private static readonly IReadOnlyList<TbankExpectedStepStates> ManualScenarioSteps =
        Steps.Where(x => !string.Equals(x.FileName, LatestImportedReportFileName, StringComparison.OrdinalIgnoreCase)).ToArray();

    [Fact]
    public async Task PortfolioSummary_FullImportedSequence_MatchesExpectedOutputContract()
    {
        await using var harness = new TbankImportScenarioHarness();
        foreach (var step in Steps)
        {
            await harness.ImportAsync(step.FileName);
        }

        var summary = await harness.GetSummaryAsync();
        var expected = TbankExpectedState.Load("expected-imported-state.json");
        var cash = await harness.GetCashByCurrencyAsync();
        var positions = await harness.GetOpenPositionsAsync();

        AssertSummaryMatchesExpected(summary, expected);
        AssertCashMatchesExpected(summary, cash, expected.Cash);
        AssertPositionsMatchExpected(positions, expected.Positions);
        AssertOutputInvariants(summary);

        Assert.Equal("RUB", summary.ReportingCurrencyId);
        Assert.Equal("adjustingAvg", summary.ValuationMethod);
        Assert.All(summary.Cash, x => Assert.True(x.Amount >= 0m, $"Cash '{x.CurrencyId}' unexpectedly became negative."));
        Assert.DoesNotContain(
            summary.Positions,
            x => string.Equals(x.InstrumentType, nameof(InstrumentType.Currency), StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task PortfolioSummary_ManualScenario_MatchesExpectedOutputContract()
    {
        await using var harness = new TbankImportScenarioHarness();
        foreach (var step in ManualScenarioSteps)
        {
            await harness.ImportAsync(step.FileName);
        }

        await harness.ApplyManualOperationsAsync();

        var summary = await harness.GetSummaryAsync();
        var expected = TbankExpectedState.Load("expected-manual-state.json");
        var cash = await harness.GetCashByCurrencyAsync();
        var positions = await harness.GetOpenPositionsAsync();
        var positionMap = positions.ToDictionary(x => x.Ticker, StringComparer.OrdinalIgnoreCase);

        AssertSummaryMatchesExpected(summary, expected);
        AssertCashMatchesExpected(summary, cash, expected.Cash);
        AssertPositionsMatchExpected(positions, expected.Positions);
        AssertOutputInvariants(summary);

        AssertPositionCurrency(positionMap, "T", "RUB", "RUB", "RUB");
        AssertPositionCurrency(positionMap, "RU000A10ELF6", "RUB", "RUB", "RUB");
        Assert.All(summary.Cash, x => Assert.True(x.Amount >= 0m, $"Cash '{x.CurrencyId}' unexpectedly became negative."));
    }

    [Fact]
    public async Task PortfoliosSummary_SinglePortfolio_MatchesPortfolioSummary()
    {
        await using var harness = new TbankImportScenarioHarness();
        foreach (var step in Steps)
        {
            await harness.ImportAsync(step.FileName);
        }

        var summary = await harness.GetSummaryAsync();
        var portfoliosSummary = await harness.GetPortfoliosSummaryAsync();

        Assert.Equal(1, portfoliosSummary.PortfolioCount);
        Assert.Equal(summary.ReportingCurrencyId, portfoliosSummary.ReportingCurrencyId);
        Assert.Equal(summary.ValuationMethod, portfoliosSummary.ValuationMethod);
        Assert.Equal(Round2(summary.NetInflowBase), Round2(portfoliosSummary.NetInflowBase));
        Assert.Equal(Round2(summary.GrossDepositsBase), Round2(portfoliosSummary.GrossDepositsBase));
        Assert.Equal(Round2(summary.GrossWithdrawalsBase), Round2(portfoliosSummary.GrossWithdrawalsBase));
        Assert.Equal(Round2(summary.CashBase), Round2(portfoliosSummary.CashBase));
        Assert.Equal(Round2(summary.PositionsValueBase), Round2(portfoliosSummary.PositionsValueBase));
        Assert.Equal(Round2(summary.RealizedBase), Round2(portfoliosSummary.RealizedBase));
        Assert.Equal(Round2(summary.UnrealizedBase), Round2(portfoliosSummary.UnrealizedBase));
        Assert.Equal(Round2(summary.NavBase), Round2(portfoliosSummary.NavBase));
        Assert.Equal(Round2(summary.RealizedBase + summary.UnrealizedBase), Round2(portfoliosSummary.PnlBase));
    }

    [Fact]
    public async Task PortfolioPerformance_FullImportedSequence_StaysConsistentWithCurrentSummary()
    {
        await using var harness = new TbankImportScenarioHarness();
        foreach (var step in Steps)
        {
            await harness.ImportAsync(step.FileName);
        }

        var summary = await harness.GetSummaryAsync();
        var performance = (await harness.GetPerformanceAsync())
            .OrderBy(x => x.StartDate)
            .ToArray();

        Assert.NotEmpty(performance);
        Assert.Equal("2019-11", performance[0].Period);
        Assert.Equal("2026-03", performance[^1].Period);

        for (var i = 0; i < performance.Length; i++)
        {
            var point = performance[i];
            Assert.Equal("RUB", point.ReportingCurrencyId);
            Assert.Equal("adjustingAvg", point.ValuationMethod);
            Assert.Equal($"{point.StartDate:yyyy-MM}", point.Period);
            Assert.Equal(1, point.StartDate.Day);
            Assert.True(point.EndDate.Date >= point.StartDate.Date);

            if (i > 0)
            {
                Assert.True(
                    performance[i - 1].StartDate < point.StartDate,
                    $"Performance period '{point.Period}' is not strictly ordered.");
            }
        }

        Assert.Equal(Round2(summary.NavBase), Round2(performance[^1].EndNavBase));
        Assert.Equal(Round2(summary.UnrealizedBase), Round2(performance[^1].UnrealizedBase));
        Assert.Equal(
            Round2(summary.NetInflowBase),
            Round2(performance.Sum(x => x.NetInflowBase)));
    }

    [Fact]
    public async Task PortfolioSummary_FullImportedSequence_PreservesMixedCurrencyOutputForBlockedAndForeignPositions()
    {
        await using var harness = new TbankImportScenarioHarness();
        foreach (var step in Steps)
        {
            await harness.ImportAsync(step.FileName);
        }

        var summary = await harness.GetSummaryAsync();
        var positions = await harness.GetOpenPositionsAsync();
        var positionMap = positions.ToDictionary(x => x.Ticker, StringComparer.OrdinalIgnoreCase);

        Assert.DoesNotContain(
            summary.Positions,
            x => string.Equals(x.InstrumentType, nameof(InstrumentType.Currency), StringComparison.OrdinalIgnoreCase));
        Assert.Equal(
            new[] { "EUR", "RUB", "USD" },
            summary.Cash.Select(x => x.CurrencyId).OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray());

        AssertPositionCurrency(positionMap, "BTI", "USD", "USD", "RUB");
        AssertPositionCurrency(positionMap, "VALE", "USD", "USD", "RUB");
        AssertPositionCurrency(positionMap, "HRL", "RUB", "RUB", "RUB");
        AssertPositionCurrency(positionMap, "MSFT", "USD", "USD", "USD");
    }

    private static void AssertSummaryMatchesExpected(
        PortfolioSummaryDto summary,
        TbankExpectedState expected)
    {
        Assert.Equal(expected.CashBase, Round2(summary.CashBase));
        Assert.Equal(expected.PositionsValueBase, Round2(summary.PositionsValueBase));
        Assert.Equal(expected.NavBase, Round2(summary.NavBase));
        Assert.Equal(expected.RealizedBase, Round2(summary.RealizedBase));
        Assert.Equal(expected.UnrealizedBase, Round2(summary.UnrealizedBase));
        Assert.Equal(expected.NetInflowBase, Round2(summary.NetInflowBase));
    }

    private static void AssertCashMatchesExpected(
        PortfolioSummaryDto summary,
        IReadOnlyDictionary<string, decimal> cashByCurrency,
        IReadOnlyDictionary<string, decimal> expectedCash)
    {
        Assert.Equal(expectedCash.Count, summary.Cash.Count);
        Assert.Equal(expectedCash.Count, cashByCurrency.Count);

        foreach (var expected in expectedCash.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase))
        {
            Assert.True(cashByCurrency.TryGetValue(expected.Key, out var actualAmount));
            Assert.Equal(expected.Value, actualAmount);

            var summaryCash = Assert.Single(summary.Cash, x => string.Equals(x.CurrencyId, expected.Key, StringComparison.OrdinalIgnoreCase));
            Assert.Equal(expected.Value, Round2(summaryCash.Amount));
        }
    }

    private static void AssertPositionsMatchExpected(
        IReadOnlyList<TbankImportScenarioHarness.PositionSnapshotItem> actualPositions,
        IReadOnlyCollection<TbankExpectedState.PositionState> expectedPositions)
    {
        var sortedActual = actualPositions
            .OrderBy(x => x.Ticker, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.InstrumentName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var sortedExpected = expectedPositions
            .OrderBy(x => x.Ticker, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.InstrumentName, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.Equal(sortedExpected.Length, sortedActual.Length);

        for (var i = 0; i < sortedExpected.Length; i++)
        {
            Assert.Equal(sortedExpected[i].Ticker, sortedActual[i].Ticker);
            Assert.Equal(sortedExpected[i].InstrumentName, sortedActual[i].InstrumentName);
            Assert.Equal(sortedExpected[i].Quantity, sortedActual[i].Quantity);
            Assert.Equal(sortedExpected[i].LastPrice, sortedActual[i].LastPrice);
            Assert.Equal(sortedExpected[i].MarketValueBase, sortedActual[i].MarketValueBase);
            Assert.Equal(sortedExpected[i].AverageCost, sortedActual[i].AverageCost);
            Assert.Equal(sortedExpected[i].CurrencyId, sortedActual[i].CurrencyId);
            Assert.Equal(sortedExpected[i].PriceCurrencyId, sortedActual[i].PriceCurrencyId);
            Assert.Equal(sortedExpected[i].AverageCostCurrencyId, sortedActual[i].AverageCostCurrencyId);
        }
    }

    private static void AssertOutputInvariants(PortfolioSummaryDto summary)
    {
        var cashBaseFromRows = Round2(summary.Cash.Sum(x => x.AmountInBase));
        var positionsBaseFromRows = Round2(summary.Positions.Sum(x => x.MarketValueBase));
        var realizedBaseFromRows = Round2(summary.RealizedByInstrument.Sum(x => x.RealizedBase));

        Assert.Equal(Round2(summary.CashBase), cashBaseFromRows);
        Assert.Equal(Round2(summary.PositionsValueBase), positionsBaseFromRows);
        Assert.Equal(Round2(summary.RealizedBase), realizedBaseFromRows);
        Assert.Equal(
            Round2(summary.GrossDepositsBase - summary.GrossWithdrawalsBase),
            Round2(summary.NetInflowBase));
        Assert.Equal(
            Round2(summary.CashBase + summary.PositionsValueBase),
            Round2(summary.NavBase));
    }

    private static void AssertPositionCurrency(
        IReadOnlyDictionary<string, TbankImportScenarioHarness.PositionSnapshotItem> positions,
        string ticker,
        string currencyId,
        string priceCurrencyId,
        string averageCostCurrencyId)
    {
        Assert.True(positions.TryGetValue(ticker, out var position), $"Position '{ticker}' was not found.");
        Assert.Equal(currencyId, position!.CurrencyId);
        Assert.Equal(priceCurrencyId, position.PriceCurrencyId);
        Assert.Equal(averageCostCurrencyId, position.AverageCostCurrencyId);
    }

    private static decimal Round2(decimal value) => decimal.Round(value, 2, MidpointRounding.AwayFromZero);
}
