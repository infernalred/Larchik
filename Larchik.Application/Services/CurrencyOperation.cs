using Larchik.Application.Helpers;

namespace Larchik.Application.Services;

public static class CurrencyOperation
{
    private static readonly Dictionary<string, Func<int, double, double, double>> MakeCurrencyOperations = new()
    {
        { ListOperations.Add, (count, price, commission) => count * price + commission },
        { ListOperations.Withdrawal, (count, price, commission) => -(count * price + commission) },
        { ListOperations.Tax, (count, price, commission) => -(count * price + commission) },
        { ListOperations.Dividends, (count, price, commission) => count * price + commission },
        { ListOperations.Purchase, (count, price, commission) => -(count * price + commission) },
        { ListOperations.Sale, (count, price, commission) => count * price + commission },
    };

    public static double CreateCurrencyDeal(string operation, int count, double price, double commission) => MakeCurrencyOperations[operation](count, price, commission);
}