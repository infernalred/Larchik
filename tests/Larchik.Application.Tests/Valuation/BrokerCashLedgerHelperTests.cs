using Larchik.Application.Portfolios.Valuation;
using Larchik.Persistence.Entities;
using Xunit;

namespace Larchik.Application.Tests.Valuation;

public class BrokerCashLedgerHelperTests
{
    [Fact]
    public void AffectsCashBalance_TbankDfpRfpAdjustment_RemainsInCashBalance()
    {
        var operation = new Operation
        {
            Type = OperationType.CashAdjustment,
            Note = "Неттинг: DFP/RFP",
            TradeDate = new DateTime(2026, 3, 20)
        };

        Assert.True(BrokerCashLedgerHelper.AffectsCashBalance(operation, usesBrokerCashLedger: true));
    }

    [Fact]
    public void AffectsCashBalance_TbankDvpRvpAdjustment_RemainsInCashBalance()
    {
        var operation = new Operation
        {
            Type = OperationType.CashAdjustment,
            Note = "Расчет: DVP/RVP",
            TradeDate = new DateTime(2026, 3, 20)
        };

        Assert.True(BrokerCashLedgerHelper.AffectsCashBalance(operation, usesBrokerCashLedger: true));
    }

    [Fact]
    public void AffectsCashBalance_TbankPurchaseSaleAdjustment_RemainsInCashBalance()
    {
        var operation = new Operation
        {
            Type = OperationType.CashAdjustment,
            Note = "Покупка/продажа",
            TradeDate = new DateTime(2026, 3, 20)
        };

        Assert.True(BrokerCashLedgerHelper.AffectsCashBalance(operation, usesBrokerCashLedger: true));
    }

    [Fact]
    public void AffectsCashBalance_NonTbankAdjustment_RemainsInCashBalance()
    {
        var operation = new Operation
        {
            Type = OperationType.CashAdjustment,
            Note = "Неттинг: DFP/RFP",
            TradeDate = new DateTime(2026, 3, 20)
        };

        Assert.True(BrokerCashLedgerHelper.AffectsCashBalance(operation, usesBrokerCashLedger: false));
    }
}
