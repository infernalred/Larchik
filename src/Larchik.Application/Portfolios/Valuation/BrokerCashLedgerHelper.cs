using Larchik.Persistence.Entities;

namespace Larchik.Application.Portfolios.Valuation;

public static class BrokerCashLedgerHelper
{
    private static readonly string[] NonBalanceCashAdjustmentMarkers =
    [
        "DFP/RFP",
        "DVP/RVP"
    ];

    public static bool UsesBrokerCashLedger(Portfolio portfolio)
    {
        return string.Equals(portfolio.Broker?.Code, "tbank", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsImportedBrokerOperation(Operation operation, bool usesBrokerCashLedger)
    {
        return usesBrokerCashLedger && !string.IsNullOrWhiteSpace(operation.BrokerOperationKey);
    }

    public static bool IsCashEffective(Operation operation, DateTime asOfDate)
    {
        return GetCashEffectiveDate(operation) <= asOfDate.Date;
    }

    public static DateTime GetCashEffectiveDate(Operation operation)
    {
        if (string.IsNullOrWhiteSpace(operation.BrokerOperationKey))
        {
            return operation.TradeDate.Date;
        }

        return operation.InstrumentId is null
            ? operation.TradeDate.Date
            : (operation.SettlementDate ?? operation.TradeDate).Date;
    }

    public static bool AffectsCashBalance(Operation operation, bool usesBrokerCashLedger)
    {
        if (!usesBrokerCashLedger || operation.Type != OperationType.CashAdjustment)
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(operation.Note))
        {
            return true;
        }

        return !NonBalanceCashAdjustmentMarkers.Any(marker =>
            operation.Note.Contains(marker, StringComparison.OrdinalIgnoreCase));
    }
}
