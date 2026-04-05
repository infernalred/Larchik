using Larchik.Application.Operations.ImportBroker;
using Larchik.Persistence.Entities;

namespace Larchik.Application.Portfolios.Valuation;

public static class BrokerCashLedgerHelper
{
    public static bool UsesBrokerCashLedger(Portfolio portfolio)
    {
        return string.Equals(portfolio.Broker?.Code, "tbank", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsImportedBrokerOperation(Operation operation, bool usesBrokerCashLedger)
    {
        return usesBrokerCashLedger && BrokerOperationIdentityHelper.IsConfirmedImportedKey(operation.BrokerOperationKey);
    }

    public static bool IsCashEffective(Operation operation, DateTime asOfDate)
    {
        return GetCashEffectiveDate(operation) <= asOfDate.Date;
    }

    public static DateTime GetCashEffectiveDate(Operation operation)
    {
        if (!BrokerOperationIdentityHelper.IsConfirmedImportedKey(operation.BrokerOperationKey))
        {
            return operation.TradeDate.Date;
        }

        return operation.InstrumentId is null
            ? operation.TradeDate.Date
            : (operation.SettlementDate ?? operation.TradeDate).Date;
    }

    public static bool AffectsCashBalance(Operation operation, bool usesBrokerCashLedger)
    {
        // T-Bank cash ledger rows are broker-authoritative for end-of-day balances.
        // Excluding DFP/RFP or DVP/RVP breaks cash reconciliation with the report.
        return true;
    }
}
