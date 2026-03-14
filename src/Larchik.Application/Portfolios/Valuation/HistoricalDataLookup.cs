using Larchik.Persistence.Entities;

namespace Larchik.Application.Portfolios.Valuation;

/// <summary>
/// Lightweight in-memory lookup for historical prices and FX rates on or before a given date.
/// Lists are expected to be small enough to keep in-memory; callers should pre-filter to relevant instruments/currencies.
/// </summary>
public class HistoricalDataLookup
{
    private readonly Dictionary<Guid, List<Price>> _pricesByInstrument;
    private readonly Dictionary<(string Base, string Quote), List<FxRate>> _fxByPair;

    public HistoricalDataLookup(IEnumerable<Price> prices, IEnumerable<FxRate> fxRates)
    {
        _pricesByInstrument = prices
            .GroupBy(p => p.InstrumentId)
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(p => p.Date)
                      .ThenBy(p => GetProviderPriority(p.Provider))
                      .ThenByDescending(p => p.CreatedAt)
                      .ToList());

        _fxByPair = fxRates
            .GroupBy(r => (r.BaseCurrencyId.ToUpperInvariant(), r.QuoteCurrencyId.ToUpperInvariant()))
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(r => r.Date)
                      .ThenByDescending(r => r.CreatedAt)
                      .ToList());
    }

    public Price? GetPrice(Guid instrumentId, DateTime asOfDate)
    {
        if (_pricesByInstrument.TryGetValue(instrumentId, out var list))
        {
            return list.FirstOrDefault(p => p.Date.Date <= asOfDate.Date);
        }

        return null;
    }

    public decimal Convert(decimal amount, string fromCurrency, string toCurrency, DateTime asOfDate)
    {
        if (string.Equals(fromCurrency, toCurrency, StringComparison.OrdinalIgnoreCase)) return amount;

        var directKey = (fromCurrency.ToUpperInvariant(), toCurrency.ToUpperInvariant());
        if (_fxByPair.TryGetValue(directKey, out var directList))
        {
            var rate = FindRate(directList, asOfDate);
            if (rate is > 0) return amount * rate.Value;
        }

        var inverseKey = (toCurrency.ToUpperInvariant(), fromCurrency.ToUpperInvariant());
        if (_fxByPair.TryGetValue(inverseKey, out var inverseList))
        {
            var rate = FindRate(inverseList, asOfDate);
            if (rate is > 0) return amount / rate.Value;
        }

        return amount;
    }

    public decimal? GetRate(string fromCurrency, string toCurrency, DateTime asOfDate)
    {
        if (string.Equals(fromCurrency, toCurrency, StringComparison.OrdinalIgnoreCase)) return 1m;

        var directKey = (fromCurrency.ToUpperInvariant(), toCurrency.ToUpperInvariant());
        if (_fxByPair.TryGetValue(directKey, out var directList))
        {
            return FindRate(directList, asOfDate);
        }

        var inverseKey = (toCurrency.ToUpperInvariant(), fromCurrency.ToUpperInvariant());
        if (_fxByPair.TryGetValue(inverseKey, out var inverseList))
        {
            var rate = FindRate(inverseList, asOfDate);
            return rate is null or 0 ? rate : 1 / rate;
        }

        return null;
    }

    private static decimal? FindRate(IReadOnlyList<FxRate> list, DateTime asOfDate)
    {
        var match = list.FirstOrDefault(r => r.Date.Date <= asOfDate.Date);
        if (match != null) return match.Rate;

        return list.Count > 0 ? list[^1].Rate : null;
    }

    private static int GetProviderPriority(string? provider)
    {
        return provider?.ToUpperInvariant() switch
        {
            "MOEX" => 0,
            "TBANK" => 1,
            _ => 2
        };
    }
}
