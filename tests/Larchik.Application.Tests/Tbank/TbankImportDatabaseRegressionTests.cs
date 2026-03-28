using Larchik.Application.Models;
using Larchik.Application.Operations.ImportBroker;
using Larchik.Persistence.Entities;
using Xunit;

namespace Larchik.Application.Tests.Tbank;

public class TbankImportDatabaseRegressionTests
{
    private static readonly IReadOnlyDictionary<string, TbankExpectedStepStates> StepSnapshots = TbankExpectedStepStates.LoadAll();

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

            Assert.True(
                StepSnapshots.TryGetValue(expected.FileName, out var stepSnapshot),
                $"Expected step snapshot for '{expected.FileName}' was not found.");
            await AssertExpectedStateAsync(harness, stepSnapshot!.Cash, stepSnapshot.Positions);

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
        foreach (var expected in Steps)
        {
            await harness.ImportAsync(expected.FileName);
        }

        await harness.ApplyManualOperationsAsync();

        var summary = await harness.GetSummaryAsync();
        var breakdown = await harness.GetBreakdownAsync();

        Assert.Equal(2370, await harness.CountOperationsAsync());
        Assert.Equal(-11053.15m, Round2(summary.CashBase));
        Assert.Equal(1148453.56m, Round2(summary.PositionsValueBase));
        Assert.Equal(1137400.41m, Round2(summary.NavBase));
        Assert.Equal(-1357.59m, Round2(summary.RealizedBase));
        Assert.Equal(77678.77m, Round2(summary.UnrealizedBase));
        Assert.Equal(836824.85m, Round2(summary.NetInflowBase));

        var expectedBreakdown = new Dictionary<OperationType, int>
        {
            [OperationType.Buy] = 535,
            [OperationType.Sell] = 163,
            [OperationType.Dividend] = 605,
            [OperationType.Fee] = 381,
            [OperationType.Deposit] = 287,
            [OperationType.Withdraw] = 32,
            [OperationType.BondPartialRedemption] = 29,
            [OperationType.BondMaturity] = 2,
            [OperationType.ReverseSplit] = 1,
            [OperationType.CashAdjustment] = 335
        };

        Assert.Equal(expectedBreakdown.Count, breakdown.Count);
        foreach (var item in expectedBreakdown)
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
        foreach (var expected in Steps)
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
        AssertPosition(after2020, "TUSD", 250m, 138997.80m, 0.11m, "USD", "USD", "USD");
        AssertPosition(after2020, "TRUR", 200m, 2130.00m, 5.35m, "RUB", "RUB", "RUB");

        await harness.ImportAsync("broker-report-2021-01-01-2021-12-31.xlsx");
        var after2021 = await GetPositionMapAsync(harness);
        AssertPosition(after2021, "VTBR", 40000m, 3444000.00m, 0m, "RUB", "RUB", "RUB");
        AssertPosition(after2021, "VEON", 80m, 329705.26m, 1.69m, "USD", "USD", "USD");
        AssertPosition(after2021, "TUSD", 40m, 22239.65m, 0.05m, "USD", "USD", "USD");
        AssertPositionAbsent(after2021, "TRUR");

        await harness.ImportAsync("broker-report-2022-01-01-2022-12-31.xlsx");
        var after2022 = await GetPositionMapAsync(harness);
        AssertPosition(after2022, "VEON", 264m, 1088027.37m, 0.91m, "USD", "USD", "USD");
        AssertPositionAbsent(after2022, "TUSD");
        AssertPositionAbsent(after2022, "TRUR");

        await harness.ImportAsync("broker-report-2023-01-01-2023-12-31.xlsx");
        var after2023 = await GetPositionMapAsync(harness);
        AssertPositionAbsent(after2023, "VEON");

        await harness.ImportAsync("broker-report-2024-01-01-2024-12-31.xlsx");
        var after2024 = await GetPositionMapAsync(harness);
        AssertPosition(after2024, "RU000A104TM1", 1m, 1000.80m, 950.89m, "RUB", "RUB", "RUB");
        AssertPositionAbsent(after2024, "VEON");

        await harness.ImportAsync("broker-report-2025-01-01-2025-12-31.xlsx");
        var after2025 = await GetPositionMapAsync(harness);
        AssertPosition(after2025, "T", 9m, 30015.00m, 3109.63m, "RUB", "RUB", "RUB");
        AssertPosition(after2025, "TRUR", 100m, 1065.00m, 4.73m, "RUB", "RUB", "RUB");
        AssertPositionAbsent(after2025, "RU000A104TM1");

        await harness.ImportAsync("broker-report-2026-01-01-2026-03-17.xlsx");
        var after2026 = await GetPositionMapAsync(harness);
        AssertPosition(after2026, "T", 18m, 60030.00m, 3241.43m, "RUB", "RUB", "RUB");
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
        AssertPosition(after2022, "HRL", 6m, 2232.00m, 50.57m, "RUB", "RUB", "USD");
        AssertPosition(after2022, "BTI", 10m, 47678.94m, 38.56m, "USD", "USD", "USD");
        AssertPosition(after2022, "VALE", 2m, 2335.33m, 11.44m, "USD", "USD", "USD");
        AssertPosition(after2022, "MSFT", 2m, 63472.75m, 292.90m, "USD", "USD", "USD");

        await harness.ImportAsync("broker-report-2023-01-01-2023-12-31.xlsx");
        await harness.ImportAsync("broker-report-2024-01-01-2024-12-31.xlsx");

        var after2024 = await GetPositionMapAsync(harness);
        AssertPosition(after2024, "HRL", 4m, 1488.00m, 3128.57m, "RUB", "RUB", "RUB");
        AssertPosition(after2024, "BTI", 7m, 33375.26m, 2831.40m, "USD", "USD", "RUB");
        AssertPosition(after2024, "VALE", 1m, 1167.66m, 881.18m, "USD", "USD", "RUB");
        AssertPosition(after2024, "MSFT", 2m, 63472.75m, 292.90m, "USD", "USD", "USD");
    }

    [Fact]
    public async Task Import_FullFixtureSequence_ThenManualOperations_TracksManualMarch2026Adds()
    {
        await using var harness = new TbankImportScenarioHarness();
        foreach (var expected in Steps)
        {
            await harness.ImportAsync(expected.FileName);
        }

        var beforeManual = await GetPositionMapAsync(harness);
        AssertPosition(beforeManual, "T", 18m, 60030.00m, 3241.43m, "RUB", "RUB", "RUB");
        AssertPositionAbsent(beforeManual, "RU000A10ELF6");

        await harness.ApplyManualOperationsAsync();

        var afterManual = await GetPositionMapAsync(harness);
        AssertPosition(afterManual, "T", 25m, 83375.00m, 3268.27m, "RUB", "RUB", "RUB");
        AssertPosition(afterManual, "RU000A10ELF6", 10m, 9989.40m, 1000.00m, "RUB", "RUB", "RUB");
        AssertPosition(afterManual, "RU000A10BU07", 5m, 41039.96m, 7849.57m, "RUB", "RUB", "RUB");
        AssertPosition(afterManual, "RU000A107HG1", 9m, 9016.02m, 1006.48m, "RUB", "RUB", "RUB");
    }

    [Fact]
    public async Task Import_AfterManualMarch2026Operations_ReconcilesMatchingTbankRowsInsteadOfCreatingDuplicates()
    {
        await using var harness = new TbankImportScenarioHarness();
        foreach (var expected in Steps)
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
                1.5m,
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
            expectedFee: 1.5m,
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

        var createdCashOperation = Assert.Single((await harness.GetOperationsAsync()).Where(x => x.Id == operationId));
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

        var createdPositionOperation = Assert.Single((await harness.GetOperationsAsync()).Where(x => x.Id == operationId));
        Assert.StartsWith("manual:v2:", createdPositionOperation.BrokerOperationKey);

        var afterCreateMap = await GetPositionMapAsync(harness);
        AssertPosition(afterCreateMap, "T", 19m, 63365.00m, 3246.35m, "RUB", "RUB", "RUB");
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

        var createdEditableOperation = Assert.Single((await harness.GetOperationsAsync()).Where(x => x.Id == operationId));
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

        var editedOperation = Assert.Single((await harness.GetOperationsAsync()).Where(x => x.Id == operationId));
        Assert.StartsWith("manual:v2:", editedOperation.BrokerOperationKey);

        var afterEditMap = await GetPositionMapAsync(harness);
        AssertPosition(afterEditMap, "T", 20m, 66700.00m, 3251.49m, "RUB", "RUB", "RUB");
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

        var reconciledOperation = Assert.Single((await harness.GetOperationsAsync()).Where(x => x.Id == operationId));
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

        var created = Assert.Single((await harness.GetOperationsAsync()).Where(x => x.Id == operationId));
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

        var reconciled = Assert.Single((await harness.GetOperationsAsync()).Where(x => x.Id == operationId));
        Assert.StartsWith("v2:", reconciled.BrokerOperationKey);
        Assert.Equal(0.8m, reconciled.Fee);
        Assert.NotNull(reconciled.SettlementDate);
        Assert.Equal(new DateTime(2026, 3, 19), reconciled.SettlementDate!.Value.Date);
        Assert.Equal("broker-import: T buy", reconciled.Note);
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

    private sealed record ImportStepExpectation(
        string FileName,
        int ImportedOperations,
        int SkippedOperations,
        int TotalOperationsInDb,
        decimal CashBase,
        decimal PositionsValueBase,
        decimal NavBase,
        decimal RealizedBase,
        decimal UnrealizedBase,
        decimal NetInflowBase,
        Dictionary<OperationType, int> Breakdown);

    private static readonly IReadOnlyList<ImportStepExpectation> Steps =
    [
        new(
            "broker-report-2019-11-01-2019-12-31.xlsx",
            7,
            0,
            7,
            2.74m,
            1722000.00m,
            1722002.74m,
            0.00m,
            1721999.92m,
            1037.97m,
            new Dictionary<OperationType, int>
            {
                [OperationType.Buy] = 2,
                [OperationType.Fee] = 2,
                [OperationType.Deposit] = 2,
                [OperationType.CashAdjustment] = 1
            }),
        new(
            "broker-report-2020-01-01-2020-12-31.xlsx",
            18,
            0,
            25,
            32.56m,
            1863127.80m,
            1863160.35m,
            0.00m,
            1858786.93m,
            5006.54m,
            new Dictionary<OperationType, int>
            {
                [OperationType.Buy] = 7,
                [OperationType.Dividend] = 1,
                [OperationType.Fee] = 4,
                [OperationType.Deposit] = 7,
                [OperationType.CashAdjustment] = 6
            }),
        new(
            "broker-report-2021-01-01-2021-12-31.xlsx",
            125,
            0,
            150,
            114.53m,
            3903841.08m,
            3903955.61m,
            692.06m,
            3765529.62m,
            136042.99m,
            new Dictionary<OperationType, int>
            {
                [OperationType.Buy] = 68,
                [OperationType.Sell] = 2,
                [OperationType.Dividend] = 8,
                [OperationType.Fee] = 17,
                [OperationType.Deposit] = 31,
                [OperationType.Withdraw] = 2,
                [OperationType.CashAdjustment] = 22
            }),
        new(
            "broker-report-2022-01-01-2022-12-31.xlsx",
            241,
            10,
            391,
            305.36m,
            4842897.00m,
            4843202.35m,
            6334.31m,
            4548013.71m,
            272576.78m,
            new Dictionary<OperationType, int>
            {
                [OperationType.Buy] = 133,
                [OperationType.Sell] = 13,
                [OperationType.Dividend] = 12,
                [OperationType.Fee] = 68,
                [OperationType.Deposit] = 59,
                [OperationType.Withdraw] = 13,
                [OperationType.CashAdjustment] = 93
            }),
        new(
            "broker-report-2023-01-01-2023-12-31.xlsx",
            294,
            0,
            685,
            22185.76m,
            4039434.02m,
            4061619.78m,
            -2702.61m,
            3406918.73m,
            592614.49m,
            new Dictionary<OperationType, int>
            {
                [OperationType.Buy] = 196,
                [OperationType.Sell] = 21,
                [OperationType.Dividend] = 70,
                [OperationType.Fee] = 141,
                [OperationType.Deposit] = 99,
                [OperationType.Withdraw] = 13,
                [OperationType.BondPartialRedemption] = 2,
                [OperationType.ReverseSplit] = 1,
                [OperationType.CashAdjustment] = 142
            }),
        new(
            "broker-report-2024-01-01-2024-12-31.xlsx",
            741,
            4,
            1426,
            -55550.54m,
            115132.89m,
            59582.35m,
            20244.29m,
            12458.95m,
            -44840.52m,
            new Dictionary<OperationType, int>
            {
                [OperationType.Buy] = 271,
                [OperationType.Sell] = 144,
                [OperationType.Dividend] = 300,
                [OperationType.Fee] = 260,
                [OperationType.Deposit] = 187,
                [OperationType.Withdraw] = 32,
                [OperationType.BondPartialRedemption] = 17,
                [OperationType.BondMaturity] = 1,
                [OperationType.ReverseSplit] = 1,
                [OperationType.CashAdjustment] = 213
            }),
        new(
            "broker-report-2025-01-01-2025-12-31.xlsx",
            670,
            0,
            2096,
            1510.67m,
            931986.48m,
            933497.15m,
            -2627.17m,
            76949.82m,
            650854.73m,
            new Dictionary<OperationType, int>
            {
                [OperationType.Buy] = 437,
                [OperationType.Sell] = 155,
                [OperationType.Dividend] = 516,
                [OperationType.Fee] = 350,
                [OperationType.Deposit] = 270,
                [OperationType.Withdraw] = 32,
                [OperationType.BondPartialRedemption] = 28,
                [OperationType.BondMaturity] = 2,
                [OperationType.ReverseSplit] = 1,
                [OperationType.CashAdjustment] = 305
            }),
        new(
            "broker-report-2026-01-01-2026-03-17.xlsx",
            267,
            9,
            2363,
            -1443.89m,
            1115119.16m,
            1113675.27m,
            -1357.59m,
            77705.37m,
            813623.41m,
            new Dictionary<OperationType, int>
            {
                [OperationType.Buy] = 532,
                [OperationType.Sell] = 163,
                [OperationType.Dividend] = 603,
                [OperationType.Fee] = 381,
                [OperationType.Deposit] = 285,
                [OperationType.Withdraw] = 32,
                [OperationType.BondPartialRedemption] = 29,
                [OperationType.BondMaturity] = 2,
                [OperationType.ReverseSplit] = 1,
                [OperationType.CashAdjustment] = 335
            })
    ];
}
