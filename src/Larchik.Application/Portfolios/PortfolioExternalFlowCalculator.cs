using Larchik.Application.Portfolios.Valuation;
using Larchik.Persistence.Entities;

namespace Larchik.Application.Portfolios;

public static class PortfolioExternalFlowCalculator
{
    public static decimal Calculate(
        Operation operation,
        IReadOnlyDictionary<Guid, Instrument> instruments,
        HistoricalDataLookup data,
        string baseCurrency)
    {
        var amount = operation.Price != 0m ? operation.Price : operation.Quantity;
        return operation.Type switch
        {
            OperationType.Deposit => data.Convert(amount, operation.CurrencyId, baseCurrency, operation.TradeDate),
            OperationType.Withdraw => -data.Convert(amount, operation.CurrencyId, baseCurrency, operation.TradeDate),
            OperationType.TransferIn when operation.InstrumentId is null =>
                data.Convert(amount, operation.CurrencyId, baseCurrency, operation.TradeDate),
            OperationType.TransferOut when operation.InstrumentId is null =>
                -data.Convert(amount, operation.CurrencyId, baseCurrency, operation.TradeDate),
            OperationType.TransferIn when TryGetTransferValue(operation, instruments, data, baseCurrency, out var value) => value,
            OperationType.TransferOut when TryGetTransferValue(operation, instruments, data, baseCurrency, out var value) => -value,
            _ => 0m
        };
    }

    private static bool TryGetTransferValue(
        Operation operation,
        IReadOnlyDictionary<Guid, Instrument> instruments,
        HistoricalDataLookup data,
        string baseCurrency,
        out decimal value)
    {
        value = 0m;
        if (operation.InstrumentId is null || !instruments.ContainsKey(operation.InstrumentId.Value))
        {
            return false;
        }

        var price = data.GetPrice(operation.InstrumentId.Value, operation.TradeDate);
        if (price is null)
        {
            return false;
        }

        value = data.Convert(operation.Quantity * price.Value, price.CurrencyId, baseCurrency, operation.TradeDate);
        return true;
    }
}
