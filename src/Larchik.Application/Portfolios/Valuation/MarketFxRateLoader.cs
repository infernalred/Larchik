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

    public static async Task<List<FxRate>> LoadAsync(
        LarchikContext context,
        IEnumerable<string> neededCurrencies,
        CancellationToken cancellationToken)
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

        var fxRates = await context.FxRates
            .AsNoTracking()
            .Where(x => currencies.Contains(x.BaseCurrencyId) && currencies.Contains(x.QuoteCurrencyId))
            .ToListAsync(cancellationToken);

        var marketRates = await LoadMarketRatesAsync(context, currencies, cancellationToken);
        if (marketRates.Count == 0)
        {
            return fxRates;
        }

        fxRates.AddRange(marketRates);
        return fxRates;
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

            result.Add(new FxRate
            {
                Id = Guid.Empty,
                BaseCurrencyId = definition.BaseCurrencyId,
                QuoteCurrencyId = definition.QuoteCurrencyId,
                Date = sample.Date.Date,
                Rate = sample.Rate,
                Source = $"MARKET_{sample.Provider.Trim().ToUpperInvariant()}",
                CreatedAt = sample.CreatedAt ?? sample.Date.Date
            });
        }

        return result;
    }

    private static async Task<List<FxRate>> LoadMarketRatesAsync(
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
            return [];
        }

        var codes = relevantDefinitions
            .SelectMany(x => x.Codes)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var instruments = await context.Instruments
            .AsNoTracking()
            .Where(x => x.Type == InstrumentType.Currency)
            .Where(x => codes.Contains(x.Ticker.ToUpper()) || codes.Contains(x.Isin.ToUpper()))
            .Select(x => new { x.Id, x.Ticker, x.Isin })
            .ToListAsync(cancellationToken);

        if (instruments.Count == 0)
        {
            return [];
        }

        var definitionsByInstrument = instruments
            .Select(x => new
            {
                x.Id,
                Definition = ResolveDefinition(x.Ticker) ?? ResolveDefinition(x.Isin)
            })
            .Where(x => x.Definition is not null)
            .ToDictionary(x => x.Id, x => x.Definition!);

        if (definitionsByInstrument.Count == 0)
        {
            return [];
        }

        var instrumentIds = definitionsByInstrument.Keys.ToArray();
        var prices = await context.Prices
            .AsNoTracking()
            .Where(x => instrumentIds.Contains(x.InstrumentId) && x.Value > 0)
            .ToListAsync(cancellationToken);

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
