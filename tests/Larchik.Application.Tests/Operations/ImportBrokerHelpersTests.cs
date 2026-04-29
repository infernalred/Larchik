using Larchik.Application.Operations.ImportBroker;
using Larchik.Persistence.Entities;
using Xunit;

namespace Larchik.Application.Tests.Operations;

public sealed class ImportBrokerHelpersTests
{
    [Fact]
    public void BrokerImportReconciliationHelper_FindsUniqueMatchingManualOperation()
    {
        var imported = CreateOperation(OperationType.Deposit, tradeDate: new DateTime(2026, 4, 20, 0, 0, 0, DateTimeKind.Utc), price: 1000m);
        var manual = CreateOperation(
            OperationType.Deposit,
            tradeDate: new DateTime(2026, 4, 21, 0, 0, 0, DateTimeKind.Utc),
            price: 1000m,
            brokerOperationKey: "manual:v2:test:000001");

        var match = BrokerImportReconciliationHelper.TryFindManualMatch(
            "tbank",
            imported,
            [manual],
            new HashSet<Guid>());

        Assert.Equal(manual.Id, match?.Id);
    }

    [Fact]
    public void BrokerImportReconciliationHelper_DoesNotMatch_WhenCandidatesAreAmbiguous()
    {
        var imported = CreateOperation(OperationType.Deposit, tradeDate: new DateTime(2026, 4, 20, 0, 0, 0, DateTimeKind.Utc), price: 1000m);
        var manual1 = CreateOperation(
            OperationType.Deposit,
            tradeDate: new DateTime(2026, 4, 20, 0, 0, 0, DateTimeKind.Utc),
            price: 1000m,
            brokerOperationKey: "manual:v2:test:000001");
        var manual2 = CreateOperation(
            OperationType.Deposit,
            tradeDate: new DateTime(2026, 4, 21, 0, 0, 0, DateTimeKind.Utc),
            price: 1000m,
            brokerOperationKey: "manual:v2:test:000002");

        var match = BrokerImportReconciliationHelper.TryFindManualMatch(
            "tbank",
            imported,
            [manual1, manual2],
            new HashSet<Guid>());

        Assert.Null(match);
    }

    [Fact]
    public void BrokerImportReconciliationHelper_Matches_WhenDecimalsDifferWithinRoundingTolerance()
    {
        var tradeDate = new DateTime(2026, 4, 20, 0, 0, 0, DateTimeKind.Utc);
        var imported = CreateOperation(
            OperationType.Deposit,
            tradeDate: tradeDate,
            price: 1000m,
            quantity: 0m,
            brokerOperationKey: null);

        var manual = CreateOperation(
            OperationType.Deposit,
            tradeDate: tradeDate,
            price: 1000.0000004m,
            quantity: 0m,
            brokerOperationKey: "manual:v2:test:000001");

        // Deposit matching uses Price comparison only with rounding tolerance.
        var match = BrokerImportReconciliationHelper.TryFindManualMatch(
            "tbank",
            imported,
            [manual],
            new HashSet<Guid>());

        Assert.Equal(manual.Id, match?.Id);
    }

    [Fact]
    public void BrokerOperationKeyBuilder_IgnoresNoteWhitespace_AndDateKindDifferences()
    {
        var utc = CreateOperation(
            OperationType.Buy,
            tradeDate: new DateTime(2026, 4, 20, 0, 0, 0, DateTimeKind.Utc),
            settlementDate: new DateTime(2026, 4, 21, 0, 0, 0, DateTimeKind.Utc),
            quantity: 1m,
            price: 100m,
            fee: 1m,
            note: " note ");
        var unspecified = CreateOperation(
            OperationType.Buy,
            tradeDate: new DateTime(2026, 4, 20, 12, 0, 0, DateTimeKind.Unspecified),
            settlementDate: new DateTime(2026, 4, 21, 12, 0, 0, DateTimeKind.Unspecified),
            quantity: 1m,
            price: 100m,
            fee: 1m,
            note: "note");

        var hash1 = BrokerOperationKeyBuilder.BuildBaseHash(utc, "US0000000001");
        var hash2 = BrokerOperationKeyBuilder.BuildBaseHash(unspecified, "US0000000001");

        Assert.Equal(hash1, hash2);
    }

    [Fact]
    public async Task BrokerOperationIdentityHelper_BuildsNextManualOccurrence_FromExistingKeys()
    {
        await using var harness = new OperationsTestHarness();
        var portfolioId = harness.AddPortfolio("Main");
        var tradeDate = new DateTime(2026, 4, 20, 0, 0, 0, DateTimeKind.Utc);
        var operation = CreateOperation(
            OperationType.Buy,
            tradeDate,
            settlementDate: tradeDate,
            quantity: 1m,
            price: 100m,
            fee: 0m,
            brokerOperationKey: null);

        var baseHash = BrokerOperationKeyBuilder.BuildBaseHash(operation, "RU0009029540");
        harness.AddOperation(
            portfolioId,
            OperationType.Buy,
            tradeDate,
            quantity: 1m,
            price: 100m,
            brokerOperationKey: $"v2:{baseHash}:000001");
        harness.AddOperation(
            portfolioId,
            OperationType.Buy,
            tradeDate,
            quantity: 1m,
            price: 100m,
            brokerOperationKey: $"manual:v2:{baseHash}:000002");
        await harness.Context.SaveChangesAsync();

        var result = await BrokerOperationIdentityHelper.BuildProvisionalManualKeyAsync(
            harness.Context,
            portfolioId,
            "tbank",
            operation,
            "RU0009029540",
            excludeOperationId: null,
            CancellationToken.None);

        Assert.StartsWith("manual:v3:", result);
        Assert.EndsWith(":000001", result);
    }

    private static Operation CreateOperation(
        OperationType type,
        DateTime tradeDate,
        DateTime? settlementDate = null,
        decimal quantity = 0m,
        decimal price = 0m,
        decimal fee = 0m,
        string currencyId = "RUB",
        string? note = null,
        string? brokerOperationKey = null) =>
        new()
        {
            Id = Guid.NewGuid(),
            Type = type,
            TradeDate = tradeDate,
            SettlementDate = settlementDate,
            Quantity = quantity,
            Price = price,
            Fee = fee,
            CurrencyId = currencyId,
            Note = note,
            BrokerOperationKey = brokerOperationKey
        };
}
