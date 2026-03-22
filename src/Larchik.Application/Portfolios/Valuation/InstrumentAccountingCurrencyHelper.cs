using Larchik.Persistence.Entities;

namespace Larchik.Application.Portfolios.Valuation;

public static class InstrumentAccountingCurrencyHelper
{
    public static IReadOnlyDictionary<Guid, string> Build(
        IEnumerable<Operation> operations,
        IReadOnlyDictionary<Guid, Instrument> instruments,
        string baseCurrency)
    {
        var result = new Dictionary<Guid, string>();

        foreach (var group in operations
                     .Where(x => x.InstrumentId.HasValue)
                     .GroupBy(x => x.InstrumentId!.Value))
        {
            if (!instruments.TryGetValue(group.Key, out var instrument) || instrument.Type == InstrumentType.Currency)
            {
                continue;
            }

            var currencies = group
                .Select(x => (x.CurrencyId ?? string.Empty).Trim().ToUpperInvariant())
                .Where(x => x.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            result[group.Key] = currencies.Length > 1
                ? baseCurrency
                : instrument.CurrencyId;
        }

        return result;
    }

    public static string Get(
        Guid instrumentId,
        IReadOnlyDictionary<Guid, string> accountingCurrencies,
        IReadOnlyDictionary<Guid, Instrument> instruments,
        string fallbackCurrency)
    {
        if (accountingCurrencies.TryGetValue(instrumentId, out var accountingCurrency))
        {
            return accountingCurrency;
        }

        return instruments.TryGetValue(instrumentId, out var instrument)
            ? instrument.CurrencyId
            : fallbackCurrency;
    }
}
