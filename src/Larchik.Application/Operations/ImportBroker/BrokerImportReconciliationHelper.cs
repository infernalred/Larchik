using Larchik.Persistence.Entities;

namespace Larchik.Application.Operations.ImportBroker;

internal static class BrokerImportReconciliationHelper
{
    public static bool SupportsManualReconciliation(string? brokerCode) =>
        string.Equals(brokerCode, "tbank", StringComparison.OrdinalIgnoreCase);

    public static (DateTime From, DateTime To) GetManualCandidateWindow(IEnumerable<Operation> importedOperations)
    {
        var dates = importedOperations.Select(x => x.TradeDate.Date).ToArray();
        if (dates.Length == 0)
        {
            var now = DateTime.UtcNow.Date;
            return (now, now);
        }

        var from = dates.Min().AddDays(-3);
        var to = dates.Max().AddDays(3);
        return (DateTime.SpecifyKind(from, DateTimeKind.Utc), DateTime.SpecifyKind(to, DateTimeKind.Utc));
    }

    public static Operation? TryFindManualMatch(
        string? brokerCode,
        Operation imported,
        IReadOnlyCollection<Operation> manualCandidates,
        ISet<Guid> reservedIds)
    {
        if (!SupportsManualReconciliation(brokerCode))
        {
            return null;
        }

        var matches = manualCandidates
            .Where(x => !reservedIds.Contains(x.Id))
            .Where(x => IsManualMatch(imported, x))
            .ToArray();

        return matches.Length == 1 ? matches[0] : null;
    }

    public static void ApplyImportedValues(Operation target, Operation imported)
    {
        target.InstrumentId = imported.InstrumentId;
        target.Type = imported.Type;
        target.Quantity = imported.Quantity;
        target.Price = imported.Price;
        target.Fee = imported.Fee;
        target.CurrencyId = imported.CurrencyId;
        target.TradeDate = imported.TradeDate;
        target.SettlementDate = imported.SettlementDate;
        target.Note = imported.Note;
        target.BrokerOperationKey = imported.BrokerOperationKey;
        target.UpdatedAt = DateTime.UtcNow;
    }

    private static bool IsManualMatch(Operation imported, Operation manual)
    {
        if (!BrokerOperationIdentityHelper.IsManualCandidateKey(manual.BrokerOperationKey))
        {
            return false;
        }

        if (manual.Type != imported.Type)
        {
            return false;
        }

        if (!string.Equals(manual.CurrencyId, imported.CurrencyId, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (manual.InstrumentId != imported.InstrumentId)
        {
            return false;
        }

        return imported.Type switch
        {
            OperationType.Buy or OperationType.Sell =>
                SameTradeDate(imported, manual) &&
                manual.Quantity == imported.Quantity &&
                manual.Price == imported.Price,

            OperationType.BondPartialRedemption or OperationType.BondMaturity =>
                SameTradeDate(imported, manual) &&
                manual.Quantity == imported.Quantity &&
                manual.Price == imported.Price,

            OperationType.Dividend =>
                IsWithinDateTolerance(imported, manual, 3) &&
                manual.Price == imported.Price,

            OperationType.Deposit or OperationType.Withdraw or OperationType.Fee or OperationType.CashAdjustment =>
                IsWithinDateTolerance(imported, manual, 3) &&
                manual.Price == imported.Price,

            _ =>
                SameTradeDate(imported, manual) &&
                manual.Quantity == imported.Quantity &&
                manual.Price == imported.Price &&
                manual.Fee == imported.Fee
        };
    }

    private static bool SameTradeDate(Operation left, Operation right) => left.TradeDate.Date == right.TradeDate.Date;

    private static bool IsWithinDateTolerance(Operation left, Operation right, int days) =>
        Math.Abs((left.TradeDate.Date - right.TradeDate.Date).TotalDays) <= days;
}
