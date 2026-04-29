using Larchik.Application.Operations.ImportBroker;
using Larchik.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Larchik.Application.Tests.Operations;

public class ImportBrokerReportCommandHandlerTests
{
    [Fact]
    public async Task Handle_Fails_WhenPortfolioIsNotAccessible()
    {
        await using var harness = new OperationsTestHarness();
        var handler = harness.CreateImportHandler(new OperationsTestHarness.FakeBrokerReportParser("tbank", () => new BrokerReportParseResult([], [])));

        var result = await handler.Handle(
            new ImportBrokerReportCommand(Guid.NewGuid(), "tbank", new MemoryStream(), "report.xlsx"),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Портфель не найден или недоступен", result.Error);
    }

    [Fact]
    public async Task Handle_Fails_WhenParserIsMissing()
    {
        await using var harness = new OperationsTestHarness();
        var portfolioId = harness.AddPortfolio("Main");
        await harness.Context.SaveChangesAsync();
        var handler = harness.CreateImportHandler();

        var result = await handler.Handle(
            new ImportBrokerReportCommand(portfolioId, "tbank", new MemoryStream(), "report.xlsx"),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Импорт для брокера 'tbank' не настроен", result.Error);
    }

    [Fact]
    public async Task Handle_Fails_WhenParserReturnsNoOperationsAndErrors()
    {
        await using var harness = new OperationsTestHarness();
        var portfolioId = harness.AddPortfolio("Main");
        await harness.Context.SaveChangesAsync();
        var handler = harness.CreateImportHandler(
            new OperationsTestHarness.FakeBrokerReportParser("tbank", () => new BrokerReportParseResult([], ["bad file"])));

        var result = await handler.Handle(
            new ImportBrokerReportCommand(portfolioId, "tbank", new MemoryStream(), "report.xlsx"),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("bad file", result.Error);
    }

    [Fact]
    public async Task Handle_ImportsOperations_AndSchedulesRebuild()
    {
        await using var harness = new OperationsTestHarness();
        var portfolioId = harness.AddPortfolio("Main");
        var instrumentId = harness.AddInstrument("SBER", isin: "RU0009029540");
        await harness.Context.SaveChangesAsync();

        var handler = harness.CreateImportHandler(
            new OperationsTestHarness.FakeBrokerReportParser("tbank", () => BuildParseResult(
                buyInstrumentCode: "RU0009029540",
                tradeDate: new DateTime(2026, 4, 20, 0, 0, 0, DateTimeKind.Utc))));

        var result = await handler.Handle(
            new ImportBrokerReportCommand(portfolioId, "tbank", new MemoryStream(), "report.xlsx"),
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal(2, result.Value!.ImportedOperations);
        Assert.Equal(0, result.Value.SkippedOperations);

        var operations = await harness.Context.Operations.OrderBy(x => x.Type).ToListAsync();
        Assert.Equal(2, operations.Count);
        Assert.Contains(operations, x => x.Type == OperationType.Deposit && x.Price == 1000m);
        Assert.Contains(operations, x => x.Type == OperationType.Buy && x.InstrumentId == instrumentId);
        Assert.Single(harness.Recalc.Calls);
        Assert.Equal((portfolioId, new DateTime(2026, 4, 20, 0, 0, 0, DateTimeKind.Utc)), harness.Recalc.Calls[0]);
    }

    [Fact]
    public async Task Handle_ResolvesInstrumentByAlias()
    {
        await using var harness = new OperationsTestHarness();
        var portfolioId = harness.AddPortfolio("Main");
        var instrumentId = harness.AddInstrument("SBER", isin: "RU0009029540");
        harness.AddInstrumentAlias(instrumentId, "BBG004730N88");
        await harness.Context.SaveChangesAsync();

        var handler = harness.CreateImportHandler(
            new OperationsTestHarness.FakeBrokerReportParser("tbank", () => BuildParseResult(
                buyInstrumentCode: "BBG004730N88",
                tradeDate: new DateTime(2026, 4, 20, 0, 0, 0, DateTimeKind.Utc))));

        var result = await handler.Handle(
            new ImportBrokerReportCommand(portfolioId, "tbank", new MemoryStream(), "report.xlsx"),
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error);

        var importedBuy = await harness.Context.Operations
            .SingleAsync(x => x.Type == OperationType.Buy);

        Assert.Equal(instrumentId, importedBuy.InstrumentId);
    }

    [Fact]
    public async Task Handle_SkipsDuplicates_OnRepeatedImport()
    {
        await using var harness = new OperationsTestHarness();
        var portfolioId = harness.AddPortfolio("Main");
        harness.AddInstrument("SBER", isin: "RU0009029540");
        await harness.Context.SaveChangesAsync();

        BrokerReportParseResult Factory() => BuildParseResult("RU0009029540", new DateTime(2026, 4, 20, 0, 0, 0, DateTimeKind.Utc));

        var handler = harness.CreateImportHandler(new OperationsTestHarness.FakeBrokerReportParser("tbank", Factory));
        var first = await handler.Handle(new ImportBrokerReportCommand(portfolioId, "tbank", new MemoryStream(), "report.xlsx"), CancellationToken.None);
        var second = await handler.Handle(new ImportBrokerReportCommand(portfolioId, "tbank", new MemoryStream(), "report.xlsx"), CancellationToken.None);

        Assert.True(first.IsSuccess, first.Error);
        Assert.True(second.IsSuccess, second.Error);
        Assert.Equal(0, second.Value!.ImportedOperations);
        Assert.Equal(2, second.Value.SkippedOperations);
        Assert.Equal(2, await harness.Context.Operations.CountAsync());
    }

    [Fact]
    public async Task Handle_SkipsDuplicates_WhenExistingConfirmedOperationIsLegacyV2Key()
    {
        await using var harness = new OperationsTestHarness();
        var portfolioId = harness.AddPortfolio("Main");

        var tradeDate = new DateTime(2026, 4, 20, 0, 0, 0, DateTimeKind.Utc);

        // Seed confirmed legacy v2 operation with the same economic identity as the import.
        harness.AddOperation(
            portfolioId,
            OperationType.Deposit,
            tradeDate,
            quantity: 0m,
            price: 1000m,
            brokerOperationKey: "v2:legacy:000001",
            note: "legacy broker note");
        await harness.Context.SaveChangesAsync();

        var handler = harness.CreateImportHandler(
            new OperationsTestHarness.FakeBrokerReportParser("tbank", () =>
                new BrokerReportParseResult(
                    [
                        new ParsedOperation(
                            new Operation
                            {
                                Id = Guid.NewGuid(),
                                Type = OperationType.Deposit,
                                Quantity = 0m,
                                Price = 1000m,
                                Fee = 0m,
                                CurrencyId = "RUB",
                                TradeDate = tradeDate,
                                SettlementDate = tradeDate,
                                Note = "some other note",
                                CreatedAt = tradeDate,
                                UpdatedAt = tradeDate
                            },
                            null,
                            false)
                    ],
                    [])));

        var result = await handler.Handle(
            new ImportBrokerReportCommand(portfolioId, "tbank", new MemoryStream(), "report.xlsx"),
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal(0, result.Value!.ImportedOperations);
        Assert.Equal(1, result.Value.SkippedOperations);
        Assert.Equal(1, await harness.Context.Operations.CountAsync());
        Assert.Empty(harness.Recalc.Calls);
    }

    [Fact]
    public async Task Handle_SkipsDuplicates_WhenExistingLegacyV2CashRowHasOldFloatArtifact()
    {
        await using var harness = new OperationsTestHarness();
        var portfolioId = harness.AddPortfolio("Main");

        var tradeDate = new DateTime(2026, 4, 20, 0, 0, 0, DateTimeKind.Utc);

        // Seed confirmed legacy v2 cash row with the old Excel float artifact value.
        var legacyArtifact = 16514.759999999998m;
        harness.AddOperation(
            portfolioId,
            OperationType.Deposit,
            tradeDate,
            quantity: 0m,
            price: legacyArtifact,
            brokerOperationKey: "v2:legacy:000001",
            note: "legacy artifact");
        await harness.Context.SaveChangesAsync();

        // Reimport the same cash row, but with the corrected rounded value.
        var handler = harness.CreateImportHandler(
            new OperationsTestHarness.FakeBrokerReportParser("tbank", () =>
                new BrokerReportParseResult(
                    [
                        new ParsedOperation(
                            new Operation
                            {
                                Id = Guid.NewGuid(),
                                Type = OperationType.Deposit,
                                Quantity = 0m,
                                Price = 16514.76m,
                                Fee = 0m,
                                CurrencyId = "RUB",
                                TradeDate = tradeDate,
                                SettlementDate = tradeDate,
                                Note = "some other note",
                                CreatedAt = tradeDate,
                                UpdatedAt = tradeDate
                            },
                            null,
                            false)
                    ],
                    [])));

        var result = await handler.Handle(
            new ImportBrokerReportCommand(portfolioId, "tbank", new MemoryStream(), "report.xlsx"),
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal(0, result.Value!.ImportedOperations);
        Assert.Equal(1, result.Value.SkippedOperations);
        Assert.Equal(1, await harness.Context.Operations.CountAsync());
        Assert.Empty(harness.Recalc.Calls);
    }

    [Fact]
    public async Task Handle_SkipsLegacyConfirmedDuplicate_BeforeManualReconciliation()
    {
        await using var harness = new OperationsTestHarness();
        var portfolioId = harness.AddPortfolio("Main");

        var tradeDate = new DateTime(2026, 4, 20, 0, 0, 0, DateTimeKind.Utc);

        var legacyConfirmedId = harness.AddOperation(
            portfolioId,
            OperationType.Deposit,
            tradeDate,
            quantity: 0m,
            price: 1000m,
            brokerOperationKey: "v2:legacy:000001",
            note: "legacy confirmed");

        var manualCandidateId = harness.AddOperation(
            portfolioId,
            OperationType.Deposit,
            tradeDate,
            quantity: 0m,
            price: 1000m,
            brokerOperationKey: "manual:v2:manual:000001",
            note: "manual provisional");

        await harness.Context.SaveChangesAsync();

        var handler = harness.CreateImportHandler(
            new OperationsTestHarness.FakeBrokerReportParser("tbank", () =>
                new BrokerReportParseResult(
                    [
                        new ParsedOperation(
                            new Operation
                            {
                                Id = Guid.NewGuid(),
                                Type = OperationType.Deposit,
                                Quantity = 0m,
                                Price = 1000m,
                                Fee = 0m,
                                CurrencyId = "RUB",
                                TradeDate = tradeDate,
                                SettlementDate = tradeDate,
                                Note = "imported note",
                                CreatedAt = tradeDate,
                                UpdatedAt = tradeDate
                            },
                            null,
                            false)
                    ],
                    [])));

        var result = await handler.Handle(
            new ImportBrokerReportCommand(portfolioId, "tbank", new MemoryStream(), "report.xlsx"),
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal(0, result.Value!.ImportedOperations);
        Assert.Equal(1, result.Value.SkippedOperations);
        Assert.Equal(2, await harness.Context.Operations.CountAsync());
        Assert.Empty(harness.Recalc.Calls);

        var legacyConfirmed = await harness.Context.Operations.SingleAsync(x => x.Id == legacyConfirmedId);
        var manualCandidate = await harness.Context.Operations.SingleAsync(x => x.Id == manualCandidateId);

        Assert.StartsWith("v2:", legacyConfirmed.BrokerOperationKey, StringComparison.Ordinal);
        Assert.StartsWith("manual:v2:", manualCandidate.BrokerOperationKey, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Handle_DoesNotBlockInsertingNewV3Occurrence_WhenExactKeyDuplicateIsSkipped()
    {
        await using var harness = new OperationsTestHarness();
        var portfolioId = harness.AddPortfolio("Main");
        await harness.Context.SaveChangesAsync();

        // Import two identical deposits -> Prepare assigns occurrences 000001 and 000002.
        // After first import, remove only occurrence 000002 and reimport:
        // the importer must insert 000002 (exact v3:000001 duplicate should not cause compatibility map to block it).
        var tradeDate = new DateTime(2026, 4, 20, 0, 0, 0, DateTimeKind.Utc);

        BrokerReportParseResult Parser() => new(
            [
                new ParsedOperation(new Operation
                {
                    Id = Guid.NewGuid(),
                    Type = OperationType.Deposit,
                    Quantity = 0m,
                    Price = 1000m,
                    Fee = 0m,
                    CurrencyId = "RUB",
                    TradeDate = tradeDate,
                    SettlementDate = tradeDate,
                    CreatedAt = tradeDate,
                    UpdatedAt = tradeDate
                }, null, false),
                new ParsedOperation(new Operation
                {
                    Id = Guid.NewGuid(),
                    Type = OperationType.Deposit,
                    Quantity = 0m,
                    Price = 1000m,
                    Fee = 0m,
                    CurrencyId = "RUB",
                    TradeDate = tradeDate,
                    SettlementDate = tradeDate,
                    CreatedAt = tradeDate,
                    UpdatedAt = tradeDate
                }, null, false)
            ],
            []);

        var handler = harness.CreateImportHandler(new OperationsTestHarness.FakeBrokerReportParser("tbank", Parser));

        var first = await handler.Handle(
            new ImportBrokerReportCommand(portfolioId, "tbank", new MemoryStream(), "report.xlsx"),
            CancellationToken.None);

        Assert.True(first.IsSuccess, first.Error);
        Assert.Equal(2, first.Value!.ImportedOperations);
        Assert.Equal(0, first.Value.SkippedOperations);
        Assert.Equal(2, await harness.Context.Operations.CountAsync());

        // Remove occurrence 000002 only.
        var trackedToRemove = harness.Context.ChangeTracker.Entries<Operation>()
            .Select(e => e.Entity)
            .FirstOrDefault(x =>
                x.BrokerOperationKey != null &&
                x.BrokerOperationKey.EndsWith(":000002", StringComparison.Ordinal));

        trackedToRemove ??= (await harness.Context.Operations
            .AsNoTracking()
            .ToListAsync())
            .Single(x =>
                x.BrokerOperationKey != null &&
                x.BrokerOperationKey.EndsWith(":000002", StringComparison.Ordinal));

        // Remove the already-tracked instance when possible, to avoid EF identity conflicts.
        harness.Context.Operations.Remove(trackedToRemove);
        await harness.Context.SaveChangesAsync();
        harness.Recalc.Calls.Clear(); // no rebuild expected from DB-only removal

        var second = await handler.Handle(
            new ImportBrokerReportCommand(portfolioId, "tbank", new MemoryStream(), "report.xlsx"),
            CancellationToken.None);

        Assert.True(second.IsSuccess, second.Error);
        Assert.Equal(1, second.Value!.ImportedOperations);
        Assert.Equal(1, second.Value.SkippedOperations);
        Assert.Equal(2, await harness.Context.Operations.CountAsync());
    }

    [Fact]
    public async Task Handle_Fails_WhenTickerIsAmbiguous()
    {
        await using var harness = new OperationsTestHarness();
        var portfolioId = harness.AddPortfolio("Main");
        harness.AddInstrument("SBER", isin: "RU0009029540");
        harness.AddInstrument("SBER", isin: "RU000A0JX0J2");
        await harness.Context.SaveChangesAsync();

        var handler = harness.CreateImportHandler(
            new OperationsTestHarness.FakeBrokerReportParser("tbank", () => BuildParseResult(
                buyInstrumentCode: "SBER",
                tradeDate: new DateTime(2026, 4, 20, 0, 0, 0, DateTimeKind.Utc))));

        var result = await handler.Handle(
            new ImportBrokerReportCommand(portfolioId, "tbank", new MemoryStream(), "report.xlsx"),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("Найдено несколько инструментов с тикером SBER. Используйте уникальный ISIN.", result.Error);
    }

    [Fact]
    public async Task Handle_ReconcilesMatchingManualOperation()
    {
        await using var harness = new OperationsTestHarness();
        var portfolioId = harness.AddPortfolio("Main");
        var instrumentId = harness.AddInstrument("SBER", isin: "RU0009029540");
        var tradeDate = new DateTime(2026, 4, 20, 0, 0, 0, DateTimeKind.Utc);
        var manualOperationId = harness.AddOperation(
            portfolioId,
            OperationType.Buy,
            tradeDate.AddDays(1),
            instrumentId,
            quantity: 1m,
            price: 100m,
            currencyId: "RUB",
            note: "manual");
        await harness.Context.SaveChangesAsync();

        var handler = harness.CreateImportHandler(
            new OperationsTestHarness.FakeBrokerReportParser("tbank", () => new BrokerReportParseResult(
            [
                new ParsedOperation(new Operation
                {
                    Id = Guid.NewGuid(),
                    Type = OperationType.Buy,
                    Quantity = 1m,
                    Price = 100m,
                    Fee = 0m,
                    CurrencyId = "RUB",
                    TradeDate = tradeDate.AddDays(1),
                    SettlementDate = tradeDate.AddDays(1),
                    CreatedAt = tradeDate.AddDays(1),
                    UpdatedAt = tradeDate.AddDays(1)
                }, "RU0009029540", true)
            ], [])));

        var result = await handler.Handle(
            new ImportBrokerReportCommand(portfolioId, "tbank", new MemoryStream(), "report.xlsx"),
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal(0, result.Value!.ImportedOperations);
        Assert.Equal(0, result.Value.SkippedOperations);

        var operations = await harness.Context.Operations.ToListAsync();
        Assert.Single(operations);
        Assert.Equal(manualOperationId, operations[0].Id);
        Assert.NotNull(operations[0].BrokerOperationKey);
        Assert.True(
            operations[0].BrokerOperationKey.StartsWith("v2:", StringComparison.Ordinal) ||
            operations[0].BrokerOperationKey.StartsWith("v3:", StringComparison.Ordinal));
        Assert.Single(harness.Recalc.Calls);
    }

    [Fact]
    public async Task Handle_ReturnsWarnings_FromParserResult()
    {
        await using var harness = new OperationsTestHarness();
        var portfolioId = harness.AddPortfolio("Main");
        harness.AddInstrument("SBER", isin: "RU0009029540");
        await harness.Context.SaveChangesAsync();

        var warning = "Cash operation fallback to CashAdjustment at row 42: 'mystery'.";
        var handler = harness.CreateImportHandler(
            new OperationsTestHarness.FakeBrokerReportParser("tbank", () => new BrokerReportParseResult(
                BuildParseResult("RU0009029540", new DateTime(2026, 4, 20, 0, 0, 0, DateTimeKind.Utc)).Operations,
                [],
                [warning])));

        var result = await handler.Handle(
            new ImportBrokerReportCommand(portfolioId, "tbank", new MemoryStream(), "report.xlsx"),
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error);
        Assert.Single(result.Value!.Warnings);
        Assert.Equal(warning, result.Value.Warnings.Single());
    }

    [Fact]
    public async Task Handle_StrictUnknownCashMapping_Fails_WhenFallbackWarningsExist()
    {
        await using var harness = new OperationsTestHarness();
        var portfolioId = harness.AddPortfolio("Main");
        harness.AddInstrument("SBER", isin: "RU0009029540");
        await harness.Context.SaveChangesAsync();

        var warning = "Cash operation fallback to CashAdjustment at row 42: 'mystery'.";
        var handler = harness.CreateImportHandler(
            new OperationsTestHarness.FakeBrokerReportParser("tbank", () => new BrokerReportParseResult(
                BuildParseResult("RU0009029540", new DateTime(2026, 4, 20, 0, 0, 0, DateTimeKind.Utc)).Operations,
                [],
                [warning])));

        var result = await handler.Handle(
            new ImportBrokerReportCommand(portfolioId, "tbank", new MemoryStream(), "report.xlsx", StrictUnknownCashMapping: true),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(warning, result.Error);
    }

    private static BrokerReportParseResult BuildParseResult(string buyInstrumentCode, DateTime tradeDate) =>
        new(
        [
            new ParsedOperation(new Operation
            {
                Id = Guid.NewGuid(),
                Type = OperationType.Deposit,
                Quantity = 0m,
                Price = 1000m,
                Fee = 0m,
                CurrencyId = "RUB",
                TradeDate = tradeDate,
                SettlementDate = tradeDate,
                CreatedAt = tradeDate,
                UpdatedAt = tradeDate
            }, null, false),
            new ParsedOperation(new Operation
            {
                Id = Guid.NewGuid(),
                Type = OperationType.Buy,
                Quantity = 1m,
                Price = 100m,
                Fee = 0m,
                CurrencyId = "RUB",
                TradeDate = tradeDate,
                SettlementDate = tradeDate,
                CreatedAt = tradeDate,
                UpdatedAt = tradeDate
            }, buyInstrumentCode, true)
        ], []);
}
