using Larchik.Application.Models;
using Larchik.Application.Operations.ImportBroker;
using Larchik.Persistence.Entities;
using Xunit;

namespace Larchik.Application.Tests.Tbank;

public class TbankImportDatabaseRegressionTests
{
    private const string LatestImportedReportFileName = "broker-report-2026-01-01-2026-03-24.xlsx";
    private static readonly IReadOnlyList<TbankExpectedStepStates> Steps = TbankExpectedStepStates.LoadAll();
    private static readonly IReadOnlyDictionary<string, TbankExpectedStepStates> StepSnapshots =
        Steps.ToDictionary(x => x.FileName, StringComparer.OrdinalIgnoreCase);
    private static readonly IReadOnlyList<TbankExpectedStepStates> ManualScenarioSteps =
        Steps.Where(x => !string.Equals(x.FileName, LatestImportedReportFileName, StringComparison.OrdinalIgnoreCase)).ToArray();

    [Fact]
    public async Task Import_FullFixtureSequence_ProducesExpectedDatabaseStateAndSummary()
    {
        await using var harness = new TbankImportScenarioHarness();

        foreach (var expected in Steps)
        {
            var import = await harness.ImportAsync(expected.FileName);
            var summary = await harness.GetSummaryAsync();
            var breakdown = await harness.GetBreakdownAsync();

            Assert.Equal(expected.ImportedOperations, import.ImportedOperations);
            Assert.Equal(expected.SkippedOperations, import.SkippedOperations);
            Assert.Equal(expected.TotalOperationsInDb, await harness.CountOperationsAsync());

            Assert.Equal(expected.CashBase, Round2(summary.CashBase));
            Assert.Equal(expected.PositionsValueBase, Round2(summary.PositionsValueBase));
            Assert.Equal(expected.NavBase, Round2(summary.NavBase));
            Assert.Equal(expected.RealizedBase, Round2(summary.RealizedBase));
            Assert.Equal(expected.UnrealizedBase, Round2(summary.UnrealizedBase));
            Assert.Equal(expected.NetInflowBase, Round2(summary.NetInflowBase));

            await AssertExpectedStateAsync(harness, expected.Cash, expected.Positions);

            Assert.Equal(expected.Breakdown.Count, breakdown.Count);
            foreach (var breakdownItem in expected.Breakdown)
            {
                Assert.True(
                    breakdown.TryGetValue(breakdownItem.Key, out var actualCount),
                    $"Operation type '{breakdownItem.Key}' was not found after importing {expected.FileName}.");
                Assert.Equal(breakdownItem.Value, actualCount);
            }
        }
    }

    [Fact]
    public async Task Import_FullFixtureSequence_ThenManualOperations_ProducesExpectedCurrentState()
    {
        await using var harness = new TbankImportScenarioHarness();
        foreach (var expected in ManualScenarioSteps)
        {
            await harness.ImportAsync(expected.FileName);
        }

        await harness.ApplyManualOperationsAsync();

        var expectedState = TbankExpectedState.Load("expected-manual-state.json");
        var summary = await harness.GetSummaryAsync();
        var breakdown = await harness.GetBreakdownAsync();

        Assert.Equal(expectedState.OperationCount, await harness.CountOperationsAsync());
        Assert.Equal(expectedState.CashBase, Round2(summary.CashBase));
        Assert.Equal(expectedState.PositionsValueBase, Round2(summary.PositionsValueBase));
        Assert.Equal(expectedState.NavBase, Round2(summary.NavBase));
        Assert.Equal(expectedState.RealizedBase, Round2(summary.RealizedBase));
        Assert.Equal(expectedState.UnrealizedBase, Round2(summary.UnrealizedBase));
        Assert.Equal(expectedState.NetInflowBase, Round2(summary.NetInflowBase));

        Assert.Equal(expectedState.Breakdown.Count, breakdown.Count);
        foreach (var item in expectedState.Breakdown)
        {
            Assert.True(breakdown.TryGetValue(item.Key, out var actualCount));
            Assert.Equal(item.Value, actualCount);
        }
    }

    [Fact]
    public async Task Import_FullFixtureSequence_MatchesExpectedCashAndOpenPositionsSnapshot()
    {
        await using var harness = new TbankImportScenarioHarness();
        foreach (var expected in Steps)
        {
            await harness.ImportAsync(expected.FileName);
        }

        await AssertExpectedStateAsync(harness, "expected-imported-state.json");
    }

    [Fact]
    public async Task Import_FullFixtureSequence_ThenManualOperations_MatchesExpectedCashAndOpenPositionsSnapshot()
    {
        await using var harness = new TbankImportScenarioHarness();
        foreach (var expected in ManualScenarioSteps)
        {
            await harness.ImportAsync(expected.FileName);
        }

        await harness.ApplyManualOperationsAsync();

        await AssertExpectedStateAsync(harness, "expected-manual-state.json");
    }

    [Fact]
    public async Task Import_LatestReportAgain_DoesNotChangePortfolioState()
    {
        await using var harness = new TbankImportScenarioHarness();
        foreach (var expected in Steps)
        {
            await harness.ImportAsync(expected.FileName);
        }

        var operationsBefore = await harness.CountOperationsAsync();
        var summaryBefore = await harness.GetSummaryAsync();
        var cashBefore = await harness.GetCashByCurrencyAsync();
        var positionsBefore = await harness.GetOpenPositionsAsync();

        var repeatImport = await harness.ImportAsync(Steps[^1].FileName);

        var operationsAfter = await harness.CountOperationsAsync();
        var summaryAfter = await harness.GetSummaryAsync();
        var cashAfter = await harness.GetCashByCurrencyAsync();
        var positionsAfter = await harness.GetOpenPositionsAsync();

        Assert.Equal(0, repeatImport.ImportedOperations);
        Assert.True(repeatImport.SkippedOperations > 0);
        Assert.Equal(operationsBefore, operationsAfter);

        AssertSummaryEqual(summaryBefore, summaryAfter);
        AssertCashEqual(cashBefore, cashAfter);
        Assert.Equal(positionsBefore, positionsAfter);
    }

    [Fact]
    public async Task Import_FullFixtureSequence_Tracks_KeyPositionTransitionsAcrossYears()
    {
        await using var harness = new TbankImportScenarioHarness();

        await harness.ImportAsync("broker-report-2019-11-01-2019-12-31.xlsx");
        AssertPosition(
            await GetPositionMapAsync(harness),
            "VTBR",
            20000m,
            1722000.00m,
            0m,
            "RUB",
            "RUB",
            "RUB");

        await harness.ImportAsync("broker-report-2020-01-01-2020-12-31.xlsx");
        var after2020 = await GetPositionMapAsync(harness);
        AssertExpectedStepPosition("broker-report-2020-01-01-2020-12-31.xlsx", after2020, "TUSD");
        AssertExpectedStepPosition("broker-report-2020-01-01-2020-12-31.xlsx", after2020, "TRUR");

        await harness.ImportAsync("broker-report-2021-01-01-2021-12-31.xlsx");
        var after2021 = await GetPositionMapAsync(harness);
        AssertExpectedStepPosition("broker-report-2021-01-01-2021-12-31.xlsx", after2021, "VTBR");
        AssertExpectedStepPosition("broker-report-2021-01-01-2021-12-31.xlsx", after2021, "VEON");
        AssertExpectedStepPosition("broker-report-2021-01-01-2021-12-31.xlsx", after2021, "TUSD");
        AssertPositionAbsent(after2021, "TRUR");

        await harness.ImportAsync("broker-report-2022-01-01-2022-12-31.xlsx");
        var after2022 = await GetPositionMapAsync(harness);
        AssertExpectedStepPosition("broker-report-2022-01-01-2022-12-31.xlsx", after2022, "VEON");
        AssertPositionAbsent(after2022, "TUSD");
        AssertPositionAbsent(after2022, "TRUR");

        await harness.ImportAsync("broker-report-2023-01-01-2023-12-31.xlsx");
        var after2023 = await GetPositionMapAsync(harness);
        AssertPositionAbsent(after2023, "VEON");

        await harness.ImportAsync("broker-report-2024-01-01-2024-12-31.xlsx");
        var after2024 = await GetPositionMapAsync(harness);
        AssertExpectedStepPosition("broker-report-2024-01-01-2024-12-31.xlsx", after2024, "RU000A104TM1");
        AssertPositionAbsent(after2024, "VEON");

        await harness.ImportAsync("broker-report-2025-01-01-2025-12-31.xlsx");
        var after2025 = await GetPositionMapAsync(harness);
        AssertExpectedStepPosition("broker-report-2025-01-01-2025-12-31.xlsx", after2025, "T");
        AssertExpectedStepPosition("broker-report-2025-01-01-2025-12-31.xlsx", after2025, "TRUR");
        AssertPositionAbsent(after2025, "RU000A104TM1");

        await harness.ImportAsync("broker-report-2026-01-01-2026-03-17.xlsx");
        var after2026 = await GetPositionMapAsync(harness);
        AssertExpectedStepPosition("broker-report-2026-01-01-2026-03-17.xlsx", after2026, "T");
        AssertPositionAbsent(after2026, "TRUR");
        AssertPositionAbsent(after2026, "RU000A10ELF6");
    }

    [Fact]
    public async Task Import_FullFixtureSequence_Tracks_MixedCurrencyBlockedInstrumentState()
    {
        await using var harness = new TbankImportScenarioHarness();

        foreach (var fileName in new[]
                 {
                     "broker-report-2019-11-01-2019-12-31.xlsx",
                     "broker-report-2020-01-01-2020-12-31.xlsx",
                     "broker-report-2021-01-01-2021-12-31.xlsx",
                     "broker-report-2022-01-01-2022-12-31.xlsx"
                 })
        {
            await harness.ImportAsync(fileName);
        }

        var after2022 = await GetPositionMapAsync(harness);
        AssertExpectedStepPosition("broker-report-2022-01-01-2022-12-31.xlsx", after2022, "HRL");
        AssertExpectedStepPosition("broker-report-2022-01-01-2022-12-31.xlsx", after2022, "BTI");
        AssertExpectedStepPosition("broker-report-2022-01-01-2022-12-31.xlsx", after2022, "VALE");
        AssertExpectedStepPosition("broker-report-2022-01-01-2022-12-31.xlsx", after2022, "MSFT");

        await harness.ImportAsync("broker-report-2023-01-01-2023-12-31.xlsx");
        await harness.ImportAsync("broker-report-2024-01-01-2024-12-31.xlsx");

        var after2024 = await GetPositionMapAsync(harness);
        AssertExpectedStepPosition("broker-report-2024-01-01-2024-12-31.xlsx", after2024, "HRL");
        AssertExpectedStepPosition("broker-report-2024-01-01-2024-12-31.xlsx", after2024, "BTI");
        AssertExpectedStepPosition("broker-report-2024-01-01-2024-12-31.xlsx", after2024, "VALE");
        AssertExpectedStepPosition("broker-report-2024-01-01-2024-12-31.xlsx", after2024, "MSFT");
    }

    [Fact]
    public async Task Import_FullFixtureSequence_ThenManualOperations_TracksManualMarch2026Adds()
    {
        await using var harness = new TbankImportScenarioHarness();
        foreach (var expected in ManualScenarioSteps)
        {
            await harness.ImportAsync(expected.FileName);
        }

        var beforeManual = await GetPositionMapAsync(harness);
        AssertExpectedStepPosition("broker-report-2026-01-01-2026-03-17.xlsx", beforeManual, "T");
        AssertPositionAbsent(beforeManual, "RU000A10ELF6");

        await harness.ApplyManualOperationsAsync();

        var afterManual = await GetPositionMapAsync(harness);
        AssertExpectedScenarioPosition("expected-manual-state.json", afterManual, "T");
        AssertExpectedScenarioPosition("expected-manual-state.json", afterManual, "RU000A10ELF6");
        AssertExpectedScenarioPosition("expected-manual-state.json", afterManual, "RU000A10BU07");
        AssertExpectedScenarioPosition("expected-manual-state.json", afterManual, "RU000A107HG1");
    }

    [Fact]
    public async Task Import_AfterManualMarch2026Operations_ReconcilesMatchingTbankRowsInsteadOfCreatingDuplicates()
    {
        await using var harness = new TbankImportScenarioHarness();
        foreach (var expected in ManualScenarioSteps)
        {
            await harness.ImportAsync(expected.FileName);
        }

        await harness.ApplyManualOperationsAsync();

        var operationsBefore = await harness.CountOperationsAsync();

        var import = await harness.ImportSyntheticAsync(
            "tbank",
            SyntheticOperation(
                OperationType.Buy,
                "T",
                2m,
                3342m,
                0m,
                "RUB",
                new DateTime(2026, 3, 18),
                new DateTime(2026, 3, 19)),
            SyntheticOperation(
                OperationType.Buy,
                "RU000A10ELF6",
                10m,
                1000m,
                0m,
                "RUB",
                new DateTime(2026, 3, 18),
                new DateTime(2026, 3, 18)),
            SyntheticOperation(
                OperationType.Dividend,
                "RU000A107HG1",
                0m,
                125.1m,
                0m,
                "RUB",
                new DateTime(2026, 3, 18),
                new DateTime(2026, 3, 18),
                "Выплата доходов по корпоративным действиям: Тип КД: Выплата дохода по облигациям, Наименование: Газпром нефть обб3П08, ISIN: RU000A107HG1"),
            SyntheticOperation(
                OperationType.Dividend,
                "RU000A10BU07",
                0m,
                425.2m,
                0m,
                "RUB",
                new DateTime(2026, 3, 18),
                new DateTime(2026, 3, 18),
                "Выплата доходов по корпоративным действиям: Тип КД: Выплата дохода по облигациям, Наименование: Полипласт обб2П07, ISIN: RU000A10BU07"),
            SyntheticOperation(
                OperationType.Deposit,
                null,
                0m,
                16514.76m,
                0m,
                "RUB",
                new DateTime(2026, 3, 18),
                new DateTime(2026, 3, 18),
                "Пополнение счета",
                requiresInstrument: false),
            SyntheticOperation(
                OperationType.Deposit,
                null,
                0m,
                6686.68m,
                0m,
                "RUB",
                new DateTime(2026, 3, 18),
                new DateTime(2026, 3, 18),
                "Пополнение счета",
                requiresInstrument: false),
            SyntheticOperation(
                OperationType.Buy,
                "T",
                5m,
                3335.4m,
                0m,
                "RUB",
                new DateTime(2026, 3, 20),
                new DateTime(2026, 3, 23)));

        Assert.Equal(0, import.ImportedOperations);
        Assert.Equal(0, import.SkippedOperations);
        Assert.Equal(operationsBefore, await harness.CountOperationsAsync());

        var operations = await harness.GetOperationsAsync();

        AssertSingleOperation(
            operations,
            OperationType.Buy,
            "T",
            2m,
            3342m,
            new DateTime(2026, 3, 18),
            brokerOperationKeyRequired: true,
            expectedFee: 0m,
            expectedSettlementDate: new DateTime(2026, 3, 19));
        AssertSingleOperation(
            operations,
            OperationType.Buy,
            "T",
            5m,
            3335.4m,
            new DateTime(2026, 3, 20),
            brokerOperationKeyRequired: true,
            expectedFee: 0m,
            expectedSettlementDate: new DateTime(2026, 3, 23));
        AssertSingleOperation(
            operations,
            OperationType.Buy,
            "RU000A10ELF6",
            10m,
            1000m,
            new DateTime(2026, 3, 18),
            brokerOperationKeyRequired: true,
            expectedFee: 0m,
            expectedSettlementDate: new DateTime(2026, 3, 18));
        AssertSingleOperation(
            operations,
            OperationType.Dividend,
            "RU000A107HG1",
            0m,
            125.1m,
            new DateTime(2026, 3, 18),
            brokerOperationKeyRequired: true);
        AssertSingleOperation(
            operations,
            OperationType.Dividend,
            "RU000A10BU07",
            0m,
            425.2m,
            new DateTime(2026, 3, 18),
            brokerOperationKeyRequired: true);
        AssertSingleOperation(
            operations,
            OperationType.Deposit,
            null,
            0m,
            16514.76m,
            new DateTime(2026, 3, 18),
            brokerOperationKeyRequired: true);
        AssertSingleOperation(
            operations,
            OperationType.Deposit,
            null,
            0m,
            6686.68m,
            new DateTime(2026, 3, 18),
            brokerOperationKeyRequired: true);
    }

    [Fact]
    public async Task Import_2026_03_24_Report_AfterManualMarchOperations_ReconcilesRealBrokerRowsWithoutDuplicates()
    {
        await using var harness = new TbankImportScenarioHarness();
        foreach (var expected in ManualScenarioSteps)
        {
            await harness.ImportAsync(expected.FileName);
        }

        await harness.ApplyManualOperationsAsync();

        var operationsBefore = await harness.CountOperationsAsync();
        var import = await harness.ImportAsync(LatestImportedReportFileName);

        Assert.True(import.ImportedOperations > 0);
        Assert.Equal(operationsBefore + import.ImportedOperations, await harness.CountOperationsAsync());

        var operations = await harness.GetOperationsAsync();

        AssertSingleOperation(
            operations,
            OperationType.Buy,
            "T",
            2m,
            3342m,
            new DateTime(2026, 3, 18),
            brokerOperationKeyRequired: true,
            expectedFee: 0m);
        AssertSingleOperation(
            operations,
            OperationType.Buy,
            "T",
            5m,
            3335.4m,
            new DateTime(2026, 3, 20),
            brokerOperationKeyRequired: true,
            expectedFee: 0m);
        AssertSingleOperation(
            operations,
            OperationType.Buy,
            "RU000A10ELF6",
            10m,
            1000m,
            new DateTime(2026, 3, 18),
            brokerOperationKeyRequired: true,
            expectedFee: 0m);
        AssertSingleOperation(
            operations,
            OperationType.Dividend,
            "RU000A107HG1",
            0m,
            125.1m,
            new DateTime(2026, 3, 18),
            brokerOperationKeyRequired: true);
        AssertSingleOperation(
            operations,
            OperationType.Dividend,
            "RU000A10BU07",
            0m,
            425.2m,
            new DateTime(2026, 3, 18),
            brokerOperationKeyRequired: true);
        AssertSingleOperation(
            operations,
            OperationType.Deposit,
            null,
            0m,
            16514.76m,
            new DateTime(2026, 3, 18),
            brokerOperationKeyRequired: true);
        AssertSingleOperation(
            operations,
            OperationType.Deposit,
            null,
            0m,
            6686.68m,
            new DateTime(2026, 3, 18),
            brokerOperationKeyRequired: true);
    }

    [Fact]
    public async Task ManualCashOperation_CreateThenDelete_RestoresImportedPortfolioState()
    {
        await using var harness = new TbankImportScenarioHarness();
        foreach (var expected in Steps)
        {
            await harness.ImportAsync(expected.FileName);
        }

        var operationsBefore = await harness.CountOperationsAsync();
        var summaryBefore = await harness.GetSummaryAsync();
        var cashBefore = await harness.GetCashByCurrencyAsync();
        var positionsBefore = await harness.GetOpenPositionsAsync();

        var operationId = await harness.CreateOperationAsync(new OperationModel(
            InstrumentId: null,
            Type: OperationType.Deposit,
            Quantity: 0m,
            Price: 1000m,
            Fee: 0m,
            CurrencyId: "RUB",
            TradeDate: new DateTimeOffset(2026, 3, 18, 12, 0, 0, TimeSpan.Zero),
            SettlementDate: new DateTimeOffset(2026, 3, 18, 12, 0, 0, TimeSpan.Zero),
            Note: "regression: temporary cash deposit"));

        var createdCashOperation = Assert.Single(await harness.GetOperationsAsync(), x => x.Id == operationId);
        Assert.StartsWith("manual:v2:", createdCashOperation.BrokerOperationKey);

        var summaryAfterCreate = await harness.GetSummaryAsync();
        Assert.Equal(Round2(summaryBefore.CashBase + 1000m), Round2(summaryAfterCreate.CashBase));
        Assert.Equal(Round2(summaryBefore.NavBase + 1000m), Round2(summaryAfterCreate.NavBase));
        Assert.Equal(Round2(summaryBefore.NetInflowBase + 1000m), Round2(summaryAfterCreate.NetInflowBase));
        Assert.Equal(operationsBefore + 1, await harness.CountOperationsAsync());

        await harness.DeleteOperationAsync(operationId);

        var summaryAfterDelete = await harness.GetSummaryAsync();
        var cashAfterDelete = await harness.GetCashByCurrencyAsync();
        var positionsAfterDelete = await harness.GetOpenPositionsAsync();

        Assert.Equal(operationsBefore, await harness.CountOperationsAsync());
        AssertSummaryEqual(summaryBefore, summaryAfterDelete);
        AssertCashEqual(cashBefore, cashAfterDelete);
        Assert.Equal(positionsBefore, positionsAfterDelete);
    }

    [Fact]
    public async Task ManualPositionOperation_CreateThenDelete_RestoresImportedPortfolioState()
    {
        await using var harness = new TbankImportScenarioHarness();
        foreach (var expected in Steps)
        {
            await harness.ImportAsync(expected.FileName);
        }

        var operationsBefore = await harness.CountOperationsAsync();
        var summaryBefore = await harness.GetSummaryAsync();
        var cashBefore = await harness.GetCashByCurrencyAsync();
        var positionsBefore = await harness.GetOpenPositionsAsync();
        var beforeMap = await GetPositionMapAsync(harness);

        var operationId = await harness.CreateOperationAsync(new OperationModel(
            InstrumentId: harness.GetInstrumentId("T"),
            Type: OperationType.Buy,
            Quantity: 1m,
            Price: 3335m,
            Fee: 0m,
            CurrencyId: "RUB",
            TradeDate: new DateTimeOffset(2026, 3, 18, 15, 0, 0, TimeSpan.Zero),
            SettlementDate: new DateTimeOffset(2026, 3, 18, 15, 0, 0, TimeSpan.Zero),
            Note: "regression: temporary T buy"));

        var createdPositionOperation = Assert.Single(await harness.GetOperationsAsync(), x => x.Id == operationId);
        Assert.StartsWith("manual:v2:", createdPositionOperation.BrokerOperationKey);

        var afterCreateMap = await GetPositionMapAsync(harness);
        var expectedQuantity = beforeMap["T"].Quantity + 1m;
        var expectedMarketValue = Round2((beforeMap["T"].LastPrice ?? 0m) * expectedQuantity);
        var expectedAverageCost = Round2(((beforeMap["T"].AverageCost * beforeMap["T"].Quantity) + 3335m) / expectedQuantity);
        Assert.Equal(expectedQuantity, afterCreateMap["T"].Quantity);
        Assert.InRange(afterCreateMap["T"].MarketValueBase, expectedMarketValue - 0.01m, expectedMarketValue + 0.01m);
        Assert.InRange(afterCreateMap["T"].AverageCost, expectedAverageCost - 0.01m, expectedAverageCost + 0.01m);
        Assert.Equal("RUB", afterCreateMap["T"].CurrencyId);
        Assert.Equal("RUB", afterCreateMap["T"].PriceCurrencyId);
        Assert.Equal("RUB", afterCreateMap["T"].AverageCostCurrencyId);
        Assert.Equal(operationsBefore + 1, await harness.CountOperationsAsync());
        Assert.NotEqual(beforeMap["T"], afterCreateMap["T"]);

        await harness.DeleteOperationAsync(operationId);

        var summaryAfterDelete = await harness.GetSummaryAsync();
        var cashAfterDelete = await harness.GetCashByCurrencyAsync();
        var positionsAfterDelete = await harness.GetOpenPositionsAsync();

        Assert.Equal(operationsBefore, await harness.CountOperationsAsync());
        AssertSummaryEqual(summaryBefore, summaryAfterDelete);
        AssertCashEqual(cashBefore, cashAfterDelete);
        Assert.Equal(positionsBefore, positionsAfterDelete);
    }

    [Fact]
    public async Task ManualOperation_EditThenDelete_RecalculatesAndRestoresPortfolioState()
    {
        await using var harness = new TbankImportScenarioHarness();
        foreach (var expected in Steps)
        {
            await harness.ImportAsync(expected.FileName);
        }

        var operationsBefore = await harness.CountOperationsAsync();
        var summaryBefore = await harness.GetSummaryAsync();
        var cashBefore = await harness.GetCashByCurrencyAsync();
        var positionsBefore = await harness.GetOpenPositionsAsync();
        var beforeMap = await GetPositionMapAsync(harness);

        var operationId = await harness.CreateOperationAsync(new OperationModel(
            InstrumentId: harness.GetInstrumentId("T"),
            Type: OperationType.Buy,
            Quantity: 1m,
            Price: 3335m,
            Fee: 0m,
            CurrencyId: "RUB",
            TradeDate: new DateTimeOffset(2026, 3, 18, 15, 0, 0, TimeSpan.Zero),
            SettlementDate: new DateTimeOffset(2026, 3, 18, 15, 0, 0, TimeSpan.Zero),
            Note: "regression: editable T buy"));

        var createdEditableOperation = Assert.Single(await harness.GetOperationsAsync(), x => x.Id == operationId);
        Assert.StartsWith("manual:v2:", createdEditableOperation.BrokerOperationKey);

        await harness.EditOperationAsync(operationId, new OperationModel(
            InstrumentId: harness.GetInstrumentId("T"),
            Type: OperationType.Buy,
            Quantity: 2m,
            Price: 3342m,
            Fee: 0m,
            CurrencyId: "RUB",
            TradeDate: new DateTimeOffset(2026, 3, 18, 16, 0, 0, TimeSpan.Zero),
            SettlementDate: new DateTimeOffset(2026, 3, 18, 16, 0, 0, TimeSpan.Zero),
            Note: "regression: edited T buy"));

        var editedOperation = Assert.Single(await harness.GetOperationsAsync(), x => x.Id == operationId);
        Assert.StartsWith("manual:v2:", editedOperation.BrokerOperationKey);

        var afterEditMap = await GetPositionMapAsync(harness);
        var expectedQuantity = beforeMap["T"].Quantity + 2m;
        var expectedMarketValue = Round2((beforeMap["T"].LastPrice ?? 0m) * expectedQuantity);
        var expectedAverageCost = Round2(((beforeMap["T"].AverageCost * beforeMap["T"].Quantity) + (3342m * 2m)) / expectedQuantity);
        AssertPosition(afterEditMap, "T", expectedQuantity, expectedMarketValue, expectedAverageCost, "RUB", "RUB", "RUB");
        Assert.Equal(operationsBefore + 1, await harness.CountOperationsAsync());

        await harness.ImportSyntheticAsync(
            "tbank",
            SyntheticOperation(
                OperationType.Buy,
                "T",
                2m,
                3342m,
                0.8m,
                "RUB",
                new DateTime(2026, 3, 18),
                new DateTime(2026, 3, 19),
                note: "broker-import: edited T buy"));

        var reconciledOperation = Assert.Single(await harness.GetOperationsAsync(), x => x.Id == operationId);
        Assert.StartsWith("v2:", reconciledOperation.BrokerOperationKey);
        Assert.Equal(0.8m, reconciledOperation.Fee);
        Assert.NotNull(reconciledOperation.SettlementDate);
        Assert.Equal(new DateTime(2026, 3, 19), reconciledOperation.SettlementDate!.Value.Date);

        await harness.DeleteOperationAsync(operationId);

        var summaryAfterDelete = await harness.GetSummaryAsync();
        var cashAfterDelete = await harness.GetCashByCurrencyAsync();
        var positionsAfterDelete = await harness.GetOpenPositionsAsync();

        Assert.Equal(operationsBefore, await harness.CountOperationsAsync());
        AssertSummaryEqual(summaryBefore, summaryAfterDelete);
        AssertCashEqual(cashBefore, cashAfterDelete);
        Assert.Equal(positionsBefore, positionsAfterDelete);
    }

    [Fact]
    public async Task ManualBrokerAwareCreate_ThenMatchingTbankImport_ReconcilesWithoutDuplicate()
    {
        await using var harness = new TbankImportScenarioHarness();
        foreach (var expected in Steps)
        {
            await harness.ImportAsync(expected.FileName);
        }

        var operationsBefore = await harness.CountOperationsAsync();
        var operationId = await harness.CreateOperationAsync(new OperationModel(
            InstrumentId: harness.GetInstrumentId("T"),
            Type: OperationType.Buy,
            Quantity: 2m,
            Price: 3342m,
            Fee: 0m,
            CurrencyId: "RUB",
            TradeDate: new DateTimeOffset(2026, 3, 18, 10, 0, 0, TimeSpan.Zero),
            SettlementDate: new DateTimeOffset(2026, 3, 18, 10, 0, 0, TimeSpan.Zero),
            Note: "regression: broker-aware provisional key"));

        var created = Assert.Single(await harness.GetOperationsAsync(), x => x.Id == operationId);
        Assert.StartsWith("manual:v2:", created.BrokerOperationKey);

        var import = await harness.ImportSyntheticAsync(
            "tbank",
            SyntheticOperation(
                OperationType.Buy,
                "T",
                2m,
                3342m,
                0.8m,
                "RUB",
                new DateTime(2026, 3, 18),
                new DateTime(2026, 3, 19),
                note: "broker-import: T buy"));

        Assert.Equal(0, import.ImportedOperations);
        Assert.Equal(0, import.SkippedOperations);
        Assert.Equal(operationsBefore + 1, await harness.CountOperationsAsync());

        var reconciled = Assert.Single(await harness.GetOperationsAsync(), x => x.Id == operationId);
        Assert.StartsWith("v2:", reconciled.BrokerOperationKey);
        Assert.Equal(0.8m, reconciled.Fee);
        Assert.NotNull(reconciled.SettlementDate);
        Assert.Equal(new DateTime(2026, 3, 19), reconciled.SettlementDate!.Value.Date);
        Assert.Equal("broker-import: T buy", reconciled.Note);
    }

    [Fact]
    public async Task ImportedTbankBondPartialRedemption_AddsCashFromLedgerAmount()
    {
        await using var harness = new TbankImportScenarioHarness();

        var import = await harness.ImportSyntheticAsync(
            "tbank",
            SyntheticOperation(
                OperationType.BondPartialRedemption,
                "RU000A10B4A4",
                9m,
                27m,
                0m,
                "RUB",
                new DateTime(2026, 3, 17),
                new DateTime(2026, 3, 17),
                note: "Выплата доходов по корпоративным действиям: Тип КД: Частичное погашение"));

        Assert.Equal(1, import.ImportedOperations);

        var summary = await harness.GetSummaryAsync();
        var cash = await harness.GetCashByCurrencyAsync();

        Assert.Equal(243m, Round2(summary.CashBase));
        Assert.Equal(243m, cash["RUB"]);
        Assert.Empty(await harness.GetOpenPositionsAsync());
    }

    [Fact]
    public async Task ImportedTbankBondMaturity_ClosesPositionAndAddsCashFromLedgerAmount()
    {
        await using var harness = new TbankImportScenarioHarness();

        await harness.CreateOperationAsync(new OperationModel(
            InstrumentId: harness.GetInstrumentId("RU000A10B4A4"),
            Type: OperationType.Buy,
            Quantity: 1m,
            Price: 1000m,
            Fee: 0m,
            CurrencyId: "RUB",
            TradeDate: new DateTimeOffset(2026, 3, 10, 10, 0, 0, TimeSpan.Zero),
            SettlementDate: new DateTimeOffset(2026, 3, 10, 10, 0, 0, TimeSpan.Zero),
            Note: "manual seed for maturity"));

        await harness.ImportSyntheticAsync(
            "tbank",
            SyntheticOperation(
                OperationType.BondMaturity,
                "RU000A10B4A4",
                1m,
                1000m,
                0m,
                "RUB",
                new DateTime(2026, 3, 17),
                new DateTime(2026, 3, 17),
                note: "Выплата доходов по корпоративным действиям: Тип КД: Погашение в уст. срок"));

        var summary = await harness.GetSummaryAsync();
        var cash = await harness.GetCashByCurrencyAsync();
        var positions = await harness.GetOpenPositionsAsync();

        Assert.Equal(0m, Round2(summary.CashBase));
        Assert.Equal(0m, cash["RUB"]);
        Assert.Empty(positions);
    }

    [Fact]
    public async Task ImportedTbankTradeFee_IsNotDoubleCountedWhenFeeOperationExists()
    {
        await using var harness = new TbankImportScenarioHarness();

        var import = await harness.ImportSyntheticAsync(
            "tbank",
            SyntheticOperation(
                OperationType.Deposit,
                null,
                0m,
                1000m,
                0m,
                "RUB",
                new DateTime(2026, 3, 18),
                new DateTime(2026, 3, 18),
                note: "Пополнение счета",
                requiresInstrument: false),
            SyntheticOperation(
                OperationType.Buy,
                "T",
                1m,
                100m,
                1m,
                "RUB",
                new DateTime(2026, 3, 18),
                new DateTime(2026, 3, 19),
                note: "Покупка 1 акции Т-Технологии"),
            SyntheticOperation(
                OperationType.CashAdjustment,
                null,
                0m,
                -100m,
                0m,
                "RUB",
                new DateTime(2026, 3, 19),
                new DateTime(2026, 3, 19),
                note: "Покупка/продажа",
                requiresInstrument: false),
            SyntheticOperation(
                OperationType.Fee,
                null,
                0m,
                1m,
                0m,
                "RUB",
                new DateTime(2026, 3, 19),
                new DateTime(2026, 3, 19),
                note: "Комиссия за сделки",
                requiresInstrument: false));

        Assert.Equal(4, import.ImportedOperations);

        var summary = await harness.GetSummaryAsync();
        var cash = await harness.GetCashByCurrencyAsync();
        var positions = await GetPositionMapAsync(harness);

        Assert.Equal(899m, Round2(summary.CashBase));
        Assert.Equal(899m, cash["RUB"]);
        Assert.Equal(1m, positions["T"].Quantity);
    }

    private static decimal Round2(decimal value) => decimal.Round(value, 2, MidpointRounding.AwayFromZero);

    private static async Task AssertExpectedStateAsync(TbankImportScenarioHarness harness, string expectedStateFileName)
    {
        var expected = TbankExpectedState.Load(expectedStateFileName);
        await AssertExpectedStateAsync(harness, expected.Cash, expected.Positions);
    }

    private static async Task AssertExpectedStateAsync(
        TbankImportScenarioHarness harness,
        IReadOnlyDictionary<string, decimal> expectedCash,
        IReadOnlyCollection<TbankExpectedState.PositionState> expectedPositionsState)
    {
        var actualCash = await harness.GetCashByCurrencyAsync();
        var actualPositions = await harness.GetOpenPositionsAsync();

        Assert.Equal(expectedCash.Count, actualCash.Count);
        foreach (var expectedCashItem in expectedCash.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase))
        {
            Assert.True(
                actualCash.TryGetValue(expectedCashItem.Key, out var actualAmount),
                $"Cash currency '{expectedCashItem.Key}' was not found in the actual portfolio state.");
            Assert.Equal(expectedCashItem.Value, actualAmount);
        }

        var expectedPositions = expectedPositionsState
            .OrderBy(x => x.Ticker, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.InstrumentName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var sortedActualPositions = actualPositions
            .OrderBy(x => x.Ticker, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.InstrumentName, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.Equal(expectedPositions.Length, sortedActualPositions.Length);

        for (var i = 0; i < expectedPositions.Length; i++)
        {
            var expectedPosition = expectedPositions[i];
            var actualPosition = sortedActualPositions[i];

            Assert.Equal(expectedPosition.Ticker, actualPosition.Ticker);
            Assert.Equal(expectedPosition.InstrumentName, actualPosition.InstrumentName);
            Assert.Equal(expectedPosition.Quantity, actualPosition.Quantity);
            Assert.Equal(expectedPosition.LastPrice, actualPosition.LastPrice);
            Assert.Equal(expectedPosition.MarketValueBase, actualPosition.MarketValueBase);
            Assert.Equal(expectedPosition.AverageCost, actualPosition.AverageCost);
            Assert.Equal(expectedPosition.CurrencyId, actualPosition.CurrencyId);
            Assert.Equal(expectedPosition.PriceCurrencyId, actualPosition.PriceCurrencyId);
            Assert.Equal(expectedPosition.AverageCostCurrencyId, actualPosition.AverageCostCurrencyId);
        }
    }

    private static void AssertSummaryEqual(PortfolioSummaryDto expected, PortfolioSummaryDto actual)
    {
        Assert.Equal(Round2(expected.CashBase), Round2(actual.CashBase));
        Assert.Equal(Round2(expected.PositionsValueBase), Round2(actual.PositionsValueBase));
        Assert.Equal(Round2(expected.NavBase), Round2(actual.NavBase));
        Assert.Equal(Round2(expected.RealizedBase), Round2(actual.RealizedBase));
        Assert.Equal(Round2(expected.UnrealizedBase), Round2(actual.UnrealizedBase));
        Assert.Equal(Round2(expected.NetInflowBase), Round2(actual.NetInflowBase));
    }

    private static void AssertCashEqual(
        IReadOnlyDictionary<string, decimal> expected,
        IReadOnlyDictionary<string, decimal> actual)
    {
        Assert.Equal(expected.Count, actual.Count);
        foreach (var expectedCash in expected.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase))
        {
            Assert.True(
                actual.TryGetValue(expectedCash.Key, out var actualAmount),
                $"Cash currency '{expectedCash.Key}' was not found in the actual portfolio state.");
            Assert.Equal(expectedCash.Value, actualAmount);
        }
    }

    private static void AssertExpectedStepPosition(
        string fileName,
        IReadOnlyDictionary<string, TbankImportScenarioHarness.PositionSnapshotItem> positions,
        string ticker)
    {
        Assert.True(
            StepSnapshots.TryGetValue(fileName, out var snapshot),
            $"Expected step snapshot for '{fileName}' was not found.");

        var expected = snapshot!.Positions.SingleOrDefault(x => string.Equals(x.Ticker, ticker, StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(expected);
        AssertPosition(
            positions,
            ticker,
            expected!.Quantity,
            expected.MarketValueBase,
            expected.AverageCost,
            expected.CurrencyId,
            expected.PriceCurrencyId,
            expected.AverageCostCurrencyId);
    }

    private static void AssertExpectedScenarioPosition(
        string fileName,
        IReadOnlyDictionary<string, TbankImportScenarioHarness.PositionSnapshotItem> positions,
        string ticker)
    {
        var snapshot = TbankExpectedState.Load(fileName);
        var expected = snapshot.Positions.SingleOrDefault(x => string.Equals(x.Ticker, ticker, StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(expected);
        AssertPosition(
            positions,
            ticker,
            expected!.Quantity,
            expected.MarketValueBase,
            expected.AverageCost,
            expected.CurrencyId,
            expected.PriceCurrencyId,
            expected.AverageCostCurrencyId);
    }

    private static async Task<IReadOnlyDictionary<string, TbankImportScenarioHarness.PositionSnapshotItem>> GetPositionMapAsync(
        TbankImportScenarioHarness harness)
    {
        var positions = await harness.GetOpenPositionsAsync();
        return positions.ToDictionary(x => x.Ticker, StringComparer.OrdinalIgnoreCase);
    }

    private static void AssertPosition(
        IReadOnlyDictionary<string, TbankImportScenarioHarness.PositionSnapshotItem> positions,
        string ticker,
        decimal quantity,
        decimal marketValueBase,
        decimal averageCost,
        string currencyId,
        string? priceCurrencyId,
        string? averageCostCurrencyId)
    {
        Assert.True(positions.TryGetValue(ticker, out var actual), $"Position '{ticker}' was not found.");
        Assert.Equal(quantity, actual!.Quantity);
        Assert.Equal(marketValueBase, actual.MarketValueBase);
        Assert.Equal(averageCost, actual.AverageCost);
        Assert.Equal(currencyId, actual.CurrencyId);
        Assert.Equal(priceCurrencyId, actual.PriceCurrencyId);
        Assert.Equal(averageCostCurrencyId, actual.AverageCostCurrencyId);
    }

    private static void AssertPositionAbsent(
        IReadOnlyDictionary<string, TbankImportScenarioHarness.PositionSnapshotItem> positions,
        string ticker)
    {
        Assert.False(positions.ContainsKey(ticker), $"Position '{ticker}' was not expected to be open.");
    }

    private static ParsedOperation SyntheticOperation(
        OperationType type,
        string? instrumentCode,
        decimal quantity,
        decimal price,
        decimal fee,
        string currencyId,
        DateTime tradeDate,
        DateTime settlementDate,
        string? note = null,
        bool requiresInstrument = true)
    {
        var operation = new Operation
        {
            Id = Guid.NewGuid(),
            Type = type,
            Quantity = quantity,
            Price = price,
            Fee = fee,
            CurrencyId = currencyId,
            TradeDate = DateTime.SpecifyKind(tradeDate, DateTimeKind.Utc),
            SettlementDate = DateTime.SpecifyKind(settlementDate, DateTimeKind.Utc),
            Note = note,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        return new ParsedOperation(operation, instrumentCode, requiresInstrument);
    }

    private static void AssertSingleOperation(
        IReadOnlyList<TbankImportScenarioHarness.OperationSnapshotItem> operations,
        OperationType type,
        string? ticker,
        decimal quantity,
        decimal price,
        DateTime tradeDate,
        bool brokerOperationKeyRequired,
        decimal? expectedFee = null,
        DateTime? expectedSettlementDate = null)
    {
        var matches = operations.Where(x =>
                x.Type == type &&
                string.Equals(x.Ticker, ticker, StringComparison.OrdinalIgnoreCase) &&
                x.Quantity == quantity &&
                x.Price == price &&
                x.TradeDate.Date == tradeDate.Date)
            .ToArray();

        var actual = Assert.Single(matches);
        if (brokerOperationKeyRequired)
        {
            Assert.False(string.IsNullOrWhiteSpace(actual.BrokerOperationKey));
        }

        if (expectedFee is not null)
        {
            Assert.Equal(expectedFee.Value, actual.Fee);
        }

        if (expectedSettlementDate is not null)
        {
            Assert.NotNull(actual.SettlementDate);
            Assert.Equal(expectedSettlementDate.Value.Date, actual.SettlementDate!.Value.Date);
        }
    }

}
