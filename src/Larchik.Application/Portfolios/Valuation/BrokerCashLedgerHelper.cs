using Larchik.Persistence.Entities;

namespace Larchik.Application.Portfolios.Valuation;

public static class BrokerCashLedgerHelper
{
    private static readonly string[] TradeCashLedgerMarkers =
    [
        "Покупка/продажа",
        "Неттинг",
        "DFP/RFP"
    ];

    public static bool UsesBrokerCashLedger(Portfolio portfolio)
    {
        return string.Equals(portfolio.Broker?.Code, "tbank", StringComparison.OrdinalIgnoreCase);
    }

    public static bool HasTradeCashLedger(Operation operation, IReadOnlyList<Operation> operations)
    {
        if (operation.Type is not OperationType.Buy and not OperationType.Sell)
        {
            return false;
        }

        var effectiveDate = GetCashEffectiveDate(operation).Date;

        return operations.Any(x =>
            x.Type == OperationType.CashAdjustment &&
            x.TradeDate.Date == effectiveDate &&
            string.Equals(x.CurrencyId, operation.CurrencyId, StringComparison.OrdinalIgnoreCase) &&
            MatchesTradeCashLedger(x.Note));
    }

    public static bool IsCashEffective(Operation operation, DateTime asOfDate)
    {
        return GetCashEffectiveDate(operation) <= asOfDate.Date;
    }

    public static DateTime GetCashEffectiveDate(Operation operation)
    {
        return operation.InstrumentId is null
            ? operation.TradeDate.Date
            : (operation.SettlementDate ?? operation.TradeDate).Date;
    }

    private static bool MatchesTradeCashLedger(string? note)
    {
        if (string.IsNullOrWhiteSpace(note))
        {
            return false;
        }

        return TradeCashLedgerMarkers.Any(marker =>
            note.Contains(marker, StringComparison.OrdinalIgnoreCase));
    }
}
