using Larchik.Persistence.Entities;

namespace Larchik.Application.Portfolios.Valuation;

/// <summary>
/// Lightweight in-memory lookup for historical prices and FX rates on or before a given date.
/// Lists are expected to be small enough to keep in-memory; callers should pre-filter to relevant instruments/currencies.
/// </summary>
public class HistoricalDataLookup
{
    private static readonly string[] PreferredCrossCurrencies = ["RUB", "USD", "EUR"];
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
                      .ThenBy(r => GetFxSourcePriority(r.Source))
                      .ThenByDescending(r => r.CreatedAt)
                      .ToList());
    }

    public Price? GetPrice(Guid instrumentId, DateTime asOfDate)
    {
        if (!_pricesByInstrument.TryGetValue(instrumentId, out var list))
        {
            return null;
        }

        var idx = FindFirstIndexDateLeq(list, asOfDate, static p => p.Date);
        return idx < 0 ? null : list[idx];
    }

    public decimal Convert(decimal amount, string fromCurrency, string toCurrency, DateTime asOfDate)
    {
        var from = fromCurrency.ToUpperInvariant();
        var to = toCurrency.ToUpperInvariant();
        if (string.Equals(from, to, StringComparison.OrdinalIgnoreCase))
        {
            return amount;
        }

        if (TryFindDirectRate(from, to, asOfDate, out var directRate))
        {
            return amount * directRate;
        }

        if (TryFindDirectRate(to, from, asOfDate, out var inverseDirectRate))
        {
            return inverseDirectRate == 0m ? amount : amount / inverseDirectRate;
        }

        var crossRate = TryGetCrossRate(from, to, asOfDate);
        return crossRate is > 0 ? amount * crossRate.Value : amount;
    }

    public decimal? GetRate(string fromCurrency, string toCurrency, DateTime asOfDate)
    {
        var from = fromCurrency.ToUpperInvariant();
        var to = toCurrency.ToUpperInvariant();
        if (string.Equals(from, to, StringComparison.OrdinalIgnoreCase))
        {
            return 1m;
        }

        var direct = FindDirectOrInverseRate(from, to, asOfDate);
        if (direct is not null)
        {
            return direct;
        }

        return TryGetCrossRate(from, to, asOfDate);
    }

    private decimal? TryGetCrossRate(string from, string to, DateTime asOfDate)
    {
        var candidateSet = _fxByPair.Keys
            .SelectMany(x => new[] { x.Base, x.Quote })
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(x => !string.Equals(x, from, StringComparison.OrdinalIgnoreCase) &&
                        !string.Equals(x, to, StringComparison.OrdinalIgnoreCase))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var candidates = PreferredCrossCurrencies
            .Where(candidateSet.Contains)
            .Concat(candidateSet.Except(PreferredCrossCurrencies, StringComparer.OrdinalIgnoreCase).OrderBy(x => x))
            .ToArray();

        foreach (var mid in candidates)
        {
            var firstLeg = FindDirectOrInverseRate(from, mid, asOfDate);
            if (firstLeg is null or <= 0)
            {
                continue;
            }

            var secondLeg = FindDirectOrInverseRate(mid, to, asOfDate);
            if (secondLeg is null or <= 0)
            {
                continue;
            }

            return firstLeg.Value * secondLeg.Value;
        }

        return null;
    }

    private decimal? FindDirectOrInverseRate(string from, string to, DateTime asOfDate)
    {
        if (TryFindDirectRate(from, to, asOfDate, out var directRate))
        {
            return directRate;
        }

        if (TryFindDirectRate(to, from, asOfDate, out var inverseDirectRate))
        {
            return inverseDirectRate == 0m ? null : 1 / inverseDirectRate;
        }

        return null;
    }

    private bool TryFindDirectRate(string from, string to, DateTime asOfDate, out decimal rate)
    {
        rate = 0m;
        var key = (from, to);
        if (!_fxByPair.TryGetValue(key, out var list))
        {
            return false;
        }

        var resolved = FindRate(list, asOfDate);
        if (resolved is null)
        {
            return false;
        }

        rate = resolved.Value;
        return true;
    }

    private static decimal? FindRate(IReadOnlyList<FxRate> list, DateTime asOfDate)
    {
        var idx = FindFirstIndexDateLeq(list, asOfDate, static r => r.Date);
        return idx < 0 ? null : list[idx].Rate;
    }

    /// <summary>
    /// Lists are sorted by date descending (then tie-breakers). Returns the smallest index i with date &lt;= as-of (latest row still on or before as-of).
    /// </summary>
    private static int FindFirstIndexDateLeq<T>(IReadOnlyList<T> list, DateTime asOfDate, Func<T, DateTime> getDate)
    {
        var target = asOfDate.Date;
        var lo = 0;
        var hi = list.Count - 1;
        var result = -1;
        while (lo <= hi)
        {
            var mid = (lo + hi) / 2;
            if (getDate(list[mid]).Date <= target)
            {
                result = mid;
                hi = mid - 1;
            }
            else
            {
                lo = mid + 1;
            }
        }

        return result;
    }

    private static int GetProviderPriority(string? provider)
    {
        return provider?.ToUpperInvariant() switch
        {
            "TBANK" => 0,
            "MOEX" => 1,
            _ => 2
        };
    }

    private static int GetFxSourcePriority(string? source)
    {
        return source?.ToUpperInvariant() switch
        {
            "MARKET_TBANK" => 0,
            "MARKET_MOEX" => 1,
            "CBR" => 2,
            _ => 3
        };
    }
}
