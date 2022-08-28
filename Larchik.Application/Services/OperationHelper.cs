using Larchik.Application.Helpers;
using Larchik.Domain.Enum;

namespace Larchik.Application.Services;

public static class OperationHelper
{
    private static readonly Dictionary<DealKind, Func<int, decimal, decimal, decimal>> CurrencyOperations = new()
    {
        { DealKind.Add, (count, price, commission) => count * price - commission },
        { DealKind.Withdrawal, (count, price, commission) => -(count * price + commission) },
        { DealKind.Tax, (count, price, commission) => -(count * price + commission) },
        { DealKind.Dividends, (count, price, commission) => count * price - commission },
        { DealKind.Purchase, (count, price, commission) => -(count * price + commission) },
        { DealKind.Sale, (count, price, commission) => count * price - commission },
        { DealKind.Commission, (count, price, commission) => -(count * price) }
    };
    
    private static readonly Dictionary<DealKind, Func<int, int>> MakeAssetOperations = new()
    {
        { DealKind.Purchase, quantity => quantity },
        { DealKind.Sale, quantity => -quantity }
    };

    public static decimal GetAmount(DealKind type, int count, decimal price, decimal commission) => Math.Round(CurrencyOperations[type](count, price, commission), 2);
    
    public static int GetAssetQuantity(DealKind type, int quantity) => MakeAssetOperations[type](quantity);
}