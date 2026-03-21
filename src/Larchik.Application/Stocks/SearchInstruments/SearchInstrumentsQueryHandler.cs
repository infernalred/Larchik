using Larchik.Application.Helpers;
using Larchik.Application.Models;
using Larchik.Persistence.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Larchik.Application.Stocks.SearchInstruments;

public class SearchInstrumentsQueryHandler(LarchikContext context)
    : IRequestHandler<SearchInstrumentsQuery, Result<InstrumentLookupDto[]>>
{
    public async Task<Result<InstrumentLookupDto[]>> Handle(SearchInstrumentsQuery request, CancellationToken cancellationToken)
    {
        var limit = Math.Clamp(request.Limit, 1, 50);
        var input = request.Query?.Trim();
        var query = context.Instruments.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(input))
        {
            var rawKey = input.ToUpperInvariant();
            var normalizedKey = NormalizeKey(rawKey);
            var compactKey = normalizedKey.Replace(" ", string.Empty);

            // Keep the SQL part translatable and broad, then do normalized matching in memory.
            query = query.Where(x =>
                x.Ticker.ToUpper().StartsWith(rawKey) ||
                x.Isin.ToUpper().StartsWith(rawKey) ||
                (x.Figi != null && x.Figi.ToUpper().StartsWith(rawKey)) ||
                x.Name.ToUpper().Contains(rawKey) ||
                x.Name.ToUpper().Contains(normalizedKey) ||
                x.Name.ToUpper().Contains(compactKey)
            );

            var candidates = await query
                .OrderBy(x => x.Type)
                .ThenBy(x => x.Ticker)
                .ThenBy(x => x.Name)
                .Take(200)
                .Select(x => new InstrumentLookupDto
                {
                    Id = x.Id,
                    Name = x.Name,
                    Ticker = x.Ticker,
                    Isin = x.Isin,
                    Figi = x.Figi,
                    CurrencyId = x.CurrencyId
                })
                .ToArrayAsync(cancellationToken);

            var filtered = candidates
                .Where(x => Matches(x, rawKey, normalizedKey, compactKey))
                .Take(limit)
                .ToArray();

            return Result<InstrumentLookupDto[]>.Success(filtered);
        }

        var instruments = await query
            .OrderBy(x => x.Type)
            .ThenBy(x => x.Ticker)
            .ThenBy(x => x.Name)
            .Take(limit)
            .Select(x => new InstrumentLookupDto
            {
                Id = x.Id,
                Name = x.Name,
                Ticker = x.Ticker,
                Isin = x.Isin,
                Figi = x.Figi,
                CurrencyId = x.CurrencyId
            })
            .ToArrayAsync(cancellationToken);

        return Result<InstrumentLookupDto[]>.Success(instruments);
    }

    private static bool Matches(InstrumentLookupDto instrument, string rawKey, string normalizedKey, string compactKey)
    {
        return MatchesValue(instrument.Ticker, rawKey, normalizedKey, compactKey)
            || MatchesValue(instrument.Isin, rawKey, normalizedKey, compactKey)
            || MatchesValue(instrument.Figi, rawKey, normalizedKey, compactKey)
            || MatchesValue(instrument.Name, rawKey, normalizedKey, compactKey);
    }

    private static bool MatchesValue(string? value, string rawKey, string normalizedKey, string compactKey)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var upper = value.ToUpperInvariant();
        var normalized = NormalizeKey(upper);
        var compact = normalized.Replace(" ", string.Empty);

        return upper.StartsWith(rawKey)
            || upper.Contains(rawKey)
            || normalized.Contains(normalizedKey)
            || compact.Contains(compactKey);
    }

    private static string NormalizeKey(string value)
    {
        return value
            .Replace('Р', 'P')
            .Replace('р', 'p')
            .Replace('–', '-')
            .Replace('—', '-')
            .Replace("-", " ")
            .Replace("_", " ")
            .Replace(".", " ")
            .Replace("  ", " ")
            .Trim();
    }
}
