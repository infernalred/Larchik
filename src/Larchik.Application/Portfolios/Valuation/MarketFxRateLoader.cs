using Larchik.Persistence.Context;
using Larchik.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace Larchik.Application.Portfolios.Valuation;

public static class MarketFxRateLoader
{
    private static readonly MarketFxDefinition[] Definitions =
    [
        new("USD", "RUB", ["USDRUB_TOM", "USD000UTSTOM"]),
        new("EUR", "RUB", ["EURRUB_TOM", "EUR_RUB__TOM"])
    ];

    /// <summary>
    /// Instrument ids for MOEX/T-Bank currency pair quotes (e.g. USDRUB_TOM) needed for FX conversion given a currency set.
    /// </summary>
    public static async Task<Guid[]> GetMarketFxInstrumentIdsAsync(
        LarchikContext context,
        IReadOnlyCollection<string> currencies,
        CancellationToken cancellationToken)
    {
        var map = await LoadMarketFxDefinitionsByInstrumentAsync(context, currencies, cancellationToken);
        return map.Count == 0 ? [] : map.Keys.ToArray();
    }

    public static async Task<List<FxRate>> LoadAsync(
        LarchikContext context,
        IEnumerable<string> neededCurrencies,
        CancellationToken cancellationToken,
        DateTime? minRateDate = null,
        DateTime? maxRateDate = null)
    {
        var currencies = neededCurrencies
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim().ToUpperInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (currencies.Length == 0)
        {
            return [];
        }

        var fxRatesQuery = context.FxRates
            .Where(x => currencies.Contains(x.BaseCurrencyId) && currencies.Contains(x.QuoteCurrencyId));

        if (minRateDate.HasValue)
        {
            var minD = minRateDate.Value.Date;
            fxRatesQuery = fxRatesQuery.Where(x => x.Date >= minD);
        }

        if (maxRateDate.HasValue)
        {
            // Inclusive calendar day: callers often pass maxPriceDate.Date (midnight), but price rows may be intraday.
            var maxInclusive = maxRateDate.Value.Date.AddDays(1).AddTicks(-1);
            fxRatesQuery = fxRatesQuery.Where(x => x.Date <= maxInclusive);
        }

        var fxRates = await fxRatesQuery.ToListAsync(cancellationToken);

        var marketRates = await LoadMarketRatesAsync(context, currencies, cancellationToken, minRateDate, maxRateDate);
        if (marketRates.Count == 0)
        {
            return fxRates;
        }

        fxRates.AddRange(marketRates);
        return fxRates;
    }

    private static async Task<Dictionary<Guid, MarketFxDefinition>> LoadMarketFxDefinitionsByInstrumentAsync(
        LarchikContext context,
        IReadOnlyCollection<string> currencies,
        CancellationToken cancellationToken)
    {
        var relevantDefinitions = Definitions
            .Where(x =>
                currencies.Contains(x.BaseCurrencyId, StringComparer.OrdinalIgnoreCase) &&
                currencies.Contains(x.QuoteCurrencyId, StringComparer.OrdinalIgnoreCase))
            .ToArray();

        if (relevantDefinitions.Length == 0)
        {
            return new Dictionary<Guid, MarketFxDefinition>();
        }

        var codes = relevantDefinitions
            .SelectMany(x => x.Codes)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var instruments = await context.Instruments
            .Where(x => x.Type == InstrumentType.Currency)
            .Where(x => codes.Contains(x.Ticker.ToUpper()) || (x.Isin != null && codes.Contains(x.Isin.ToUpper())))
            .Select(x => new { x.Id, x.Ticker, x.Isin })
            .ToListAsync(cancellationToken);

        if (instruments.Count == 0)
        {
            return new Dictionary<Guid, MarketFxDefinition>();
        }

        return instruments
            .Select(x => new
            {
                x.Id,
                Definition = ResolveDefinition(x.Ticker) ?? ResolveDefinition(x.Isin)
            })
            .Where(x => x.Definition is not null)
            .ToDictionary(x => x.Id, x => x.Definition!);
    }

    public static List<FxRate> BuildFromSamples(IEnumerable<MarketFxSample> samples)
    {
        var result = new List<FxRate>();

        foreach (var sample in samples)
        {
            var definition = ResolveDefinition(sample.Code);
            if (definition is null || sample.Rate <= 0)
            {
                continue;
            }

            var stamp = sample.CreatedAt ?? sample.Date.Date;
            result.Add(new FxRate
            {
                Id = Guid.Empty,
                BaseCurrencyId = definition.BaseCurrencyId,
                QuoteCurrencyId = definition.QuoteCurrencyId,
                Date = sample.Date.Date,
                Rate = sample.Rate,
                Source = $"MARKET_{sample.Provider.Trim().ToUpperInvariant()}",
                CreatedAt = stamp,
                UpdatedAt = stamp
            });
        }

        return result;
    }

    private static async Task<List<FxRate>> LoadMarketRatesAsync(
        LarchikContext context,
        IReadOnlyCollection<string> currencies,
        CancellationToken cancellationToken,
        DateTime? minRateDate = null,
        DateTime? maxRateDate = null)
    {
        var definitionsByInstrument = await LoadMarketFxDefinitionsByInstrumentAsync(context, currencies, cancellationToken);
        if (definitionsByInstrument.Count == 0)
        {
            return [];
        }

        var instrumentIds = definitionsByInstrument.Keys.ToArray();
        var pricesQuery = context.Prices
            .Where(x => instrumentIds.Contains(x.InstrumentId) && x.Value > 0);

        if (minRateDate.HasValue)
        {
            var minD = minRateDate.Value.Date;
            pricesQuery = pricesQuery.Where(x => x.Date >= minD);
        }

        if (maxRateDate.HasValue)
        {
            var maxInclusive = maxRateDate.Value.Date.AddDays(1).AddTicks(-1);
            pricesQuery = pricesQuery.Where(x => x.Date <= maxInclusive);
        }

        var prices = await pricesQuery.ToListAsync(cancellationToken);

        var samples = prices
            .Where(x => definitionsByInstrument.ContainsKey(x.InstrumentId))
            .Select(x => new MarketFxSample(
                definitionsByInstrument[x.InstrumentId].Codes[0],
                x.Date,
                x.Value,
                x.Provider,
                x.UpdatedAt))
            .ToArray();

        return BuildFromSamples(samples);
    }

    private static MarketFxDefinition? ResolveDefinition(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return null;
        }

        return Definitions.FirstOrDefault(x => x.Codes.Contains(code.Trim().ToUpperInvariant(), StringComparer.OrdinalIgnoreCase));
    }

    private sealed record MarketFxDefinition(string BaseCurrencyId, string QuoteCurrencyId, string[] Codes);
}

public sealed record MarketFxSample(
    string Code,
    DateTime Date,
    decimal Rate,
    string Provider,
    DateTime? CreatedAt = null);
