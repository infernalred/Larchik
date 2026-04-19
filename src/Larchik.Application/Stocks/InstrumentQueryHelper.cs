using System.Linq.Expressions;
using Larchik.Application.Models;
using Larchik.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace Larchik.Application.Stocks;

public static class InstrumentQueryHelper
{
    public static Expression<Func<Instrument, InstrumentDto>> AdminDtoProjection => x => new InstrumentDto
    {
        Id = x.Id,
        Name = x.Name,
        Ticker = x.Ticker,
        Isin = x.Isin,
        Figi = x.Figi,
        Type = x.Type,
        CurrencyId = x.CurrencyId,
        CategoryId = x.CategoryId,
        Exchange = x.Exchange,
        Country = x.Country,
        IsTrading = x.IsTrading,
        PriceSource = x.PriceSource
    };

    public static Expression<Func<Instrument, InstrumentLookupDto>> LookupDtoProjection => x => new InstrumentLookupDto
    {
        Id = x.Id,
        Name = x.Name,
        Ticker = x.Ticker,
        Isin = x.Isin,
        Figi = x.Figi,
        CurrencyId = x.CurrencyId
    };

    public static IOrderedQueryable<Instrument> ApplyDefaultOrdering(IQueryable<Instrument> query) =>
        query
            .OrderBy(x => x.Type)
            .ThenBy(x => x.Ticker)
            .ThenBy(x => x.Name);

    public static IQueryable<Instrument> ApplyAdminSearch(IQueryable<Instrument> query, string? input)
    {
        var search = input?.Trim();
        if (string.IsNullOrWhiteSpace(search))
        {
            return query;
        }

        var pattern = $"%{search}%";
        return query.Where(x =>
            EF.Functions.ILike(x.Ticker, pattern) ||
            EF.Functions.ILike(x.Name, pattern) ||
            (x.Isin != null && EF.Functions.ILike(x.Isin, pattern)) ||
            (x.Figi != null && EF.Functions.ILike(x.Figi, pattern)) ||
            (x.Exchange != null && EF.Functions.ILike(x.Exchange, pattern)) ||
            (x.Country != null && EF.Functions.ILike(x.Country, pattern)));
    }

    public static IQueryable<Instrument> ApplyLookupCandidateSearch(IQueryable<Instrument> query, string input)
    {
        var search = input.Trim();
        var rawKey = search.ToUpperInvariant();
        var normalizedKey = NormalizeSearchKey(rawKey);
        var compactKey = normalizedKey.Replace(" ", string.Empty);
        var codePattern = $"{search}%";

        return query.Where(x =>
            EF.Functions.ILike(x.Ticker, codePattern) ||
            (x.Isin != null && EF.Functions.ILike(x.Isin, codePattern)) ||
            (x.Figi != null && EF.Functions.ILike(x.Figi, codePattern)) ||
            x.Name.ToUpper().Contains(rawKey) ||
            x.Name.ToUpper().Contains(normalizedKey) ||
            x.Name.ToUpper().Contains(compactKey));
    }

    public static bool MatchesLookup(InstrumentLookupDto instrument, string rawKey, string normalizedKey, string compactKey) =>
        MatchesValue(instrument.Ticker, rawKey, normalizedKey, compactKey)
        || MatchesValue(instrument.Isin, rawKey, normalizedKey, compactKey)
        || MatchesValue(instrument.Figi, rawKey, normalizedKey, compactKey)
        || MatchesValue(instrument.Name, rawKey, normalizedKey, compactKey);

    public static string NormalizeSearchKey(string value) =>
        value
            .Replace('Р', 'P')
            .Replace('р', 'p')
            .Replace('–', '-')
            .Replace('—', '-')
            .Replace("-", " ")
            .Replace("_", " ")
            .Replace(".", " ")
            .Replace("  ", " ")
            .Trim();

    private static bool MatchesValue(string? value, string rawKey, string normalizedKey, string compactKey)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var upper = value.ToUpperInvariant();
        var normalized = NormalizeSearchKey(upper);
        var compact = normalized.Replace(" ", string.Empty);

        return upper.StartsWith(rawKey)
            || upper.Contains(rawKey)
            || normalized.Contains(normalizedKey)
            || compact.Contains(compactKey);
    }
}
