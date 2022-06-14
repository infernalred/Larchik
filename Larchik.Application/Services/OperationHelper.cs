using Larchik.Application.Helpers;

namespace Larchik.Application.Services;

public static class OperationHelper
{
    private static readonly Dictionary<string, Func<int, decimal, decimal, decimal>> CurrencyOperations = new()
    {
        { ListOperations.Add, (count, price, commission) => count * price - commission },
        { ListOperations.Withdrawal, (count, price, commission) => -(count * price + commission) },
        { ListOperations.Tax, (count, price, commission) => -(count * price + commission) },
        { ListOperations.Dividends, (count, price, commission) => count * price - commission },
        { ListOperations.Purchase, (count, price, commission) => -(count * price + commission) },
        { ListOperations.Sale, (count, price, commission) => count * price - commission },
        { ListOperations.Commission, (count, price, commission) => -(count * price) }
    };
    
    private static readonly Dictionary<string, Func<int, int>> MakeAssetOperations = new()
    {
        { ListOperations.Purchase, quantity => quantity },
        { ListOperations.Sale, quantity => -quantity }
    };

    public static decimal GetAmount(string operation, int count, decimal price, decimal commission) => CurrencyOperations[operation](count, price, commission);
    
    public static int GetAssetQuantity(string operation, int quantity) => MakeAssetOperations[operation](quantity);
}