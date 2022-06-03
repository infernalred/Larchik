using Larchik.Application.Helpers;

namespace Larchik.Application.Services;

public static class CurrencyOperation
{
    private static readonly Dictionary<string, Func<int, decimal, decimal, decimal>> CurrencyOperations = new()
    {
        { ListOperations.Add, (count, price, commission) => count * price - commission },
        { ListOperations.Withdrawal, (count, price, commission) => -(count * price + commission) },
        { ListOperations.Tax, (count, price, commission) => -(count * price + commission) },
        { ListOperations.Dividends, (count, price, commission) => count * price - commission },
        { ListOperations.Purchase, (count, price, commission) => -(count * price + commission) },
        { ListOperations.Sale, (count, price, commission) => count * price - commission },
    };

    public static decimal GetAmount(string operation, int count, decimal price, decimal commission) => CurrencyOperations[operation](count, price, commission);
}