using Larchik.Persistence.Enum;

namespace Larchik.Application.Services;

public static class OperationHelper
{
    private static readonly Dictionary<OperationKind, Func<int, decimal, decimal, decimal>> CurrencyOperations = new()
    {
        { OperationKind.Add, (count, price, commission) => count * price - commission },
        { OperationKind.Withdrawal, (count, price, commission) => -(count * price + commission) },
        { OperationKind.Tax, (count, price, commission) => -(count * price + commission) },
        { OperationKind.Dividends, (count, price, commission) => count * price - commission },
        { OperationKind.Purchase, (count, price, commission) => -(count * price + commission) },
        { OperationKind.Sale, (count, price, commission) => count * price - commission },
        { OperationKind.Commission, (count, price, commission) => -(count * price) }
    };
    
    private static readonly Dictionary<OperationKind, Func<int, int>> MakeAssetOperations = new()
    {
        { OperationKind.Purchase, quantity => quantity },
        { OperationKind.Sale, quantity => -quantity }
    };

    public static decimal GetAmount(OperationKind type, int count, decimal price, decimal commission) => Math.Round(CurrencyOperations[type](count, price, commission), 2);
    
    public static int GetAssetQuantity(OperationKind type, int quantity) => MakeAssetOperations[type](quantity);
}