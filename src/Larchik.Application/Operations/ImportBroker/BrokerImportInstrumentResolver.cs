using Larchik.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace Larchik.Application.Operations.ImportBroker;

internal static class BrokerImportInstrumentResolver
{
    public static async Task<BrokerImportInstrumentResolution> ResolveAsync(
        LarchikContext context,
        IReadOnlyCollection<ParsedOperation> parsedOperations,
        CancellationToken cancellationToken)
    {
        var normalizedCodes = parsedOperations
            .Where(x => x.RequiresInstrument && !string.IsNullOrWhiteSpace(x.InstrumentCode))
            .Select(x => NormalizeCode(x.InstrumentCode)!)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (normalizedCodes.Length == 0)
        {
            return BrokerImportInstrumentResolution.Empty;
        }

        var aliasEntries = await context.InstrumentAliases
            .Where(x => normalizedCodes.Contains(x.NormalizedAliasCode))
            .ToListAsync(cancellationToken);

        var aliasMap = aliasEntries
            .GroupBy(x => x.NormalizedAliasCode, StringComparer.Ordinal)
            .ToDictionary(x => x.Key, x => x.First().InstrumentId, StringComparer.Ordinal);

        var aliasInstrumentIds = aliasEntries
            .Select(x => x.InstrumentId)
            .Distinct()
            .ToArray();

        var instruments = await context.Instruments
            .Where(x =>
                normalizedCodes.Contains(x.Ticker.ToUpper()) ||
                (x.Isin != null && normalizedCodes.Contains(x.Isin.ToUpper())) ||
                aliasInstrumentIds.Contains(x.Id))
            .ToListAsync(cancellationToken);

        var isinMap = instruments
            .Where(x => !string.IsNullOrWhiteSpace(x.Isin))
            .GroupBy(x => NormalizeCode(x.Isin)!, StringComparer.Ordinal)
            .ToDictionary(x => x.Key, x => x.First().Id, StringComparer.Ordinal);

        var tickerGroups = instruments
            .Where(x => !string.IsNullOrWhiteSpace(x.Ticker))
            .GroupBy(x => NormalizeCode(x.Ticker)!, StringComparer.Ordinal)
            .ToArray();

        var ambiguousTickers = tickerGroups
            .Where(x => x.Select(i => i.Id).Distinct().Skip(1).Any())
            .Select(x => x.Key)
            .ToHashSet(StringComparer.Ordinal);

        var tickerMap = tickerGroups
            .Where(x => !ambiguousTickers.Contains(x.Key))
            .ToDictionary(x => x.Key, x => x.First().Id, StringComparer.Ordinal);

        var canonicalInstrumentCodeById = instruments
            .GroupBy(x => x.Id)
            .ToDictionary(
                x => x.Key,
                x => !string.IsNullOrWhiteSpace(x.First().Isin)
                    ? x.First().Isin!
                    : x.First().Ticker,
                EqualityComparer<Guid>.Default);

        var unresolvedCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var ambiguousCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var parsed in parsedOperations.Where(x => x.RequiresInstrument))
        {
            var normalizedCode = NormalizeCode(parsed.InstrumentCode);
            var displayCode = parsed.InstrumentCode ?? "UNKNOWN";

            if (normalizedCode is null)
            {
                unresolvedCodes.Add(displayCode);
                continue;
            }

            if (aliasMap.ContainsKey(normalizedCode) || isinMap.ContainsKey(normalizedCode))
            {
                continue;
            }

            if (ambiguousTickers.Contains(normalizedCode))
            {
                ambiguousCodes.Add(displayCode);
                continue;
            }

            if (!tickerMap.ContainsKey(normalizedCode))
            {
                unresolvedCodes.Add(displayCode);
            }
        }

        return new BrokerImportInstrumentResolution(
            aliasMap,
            isinMap,
            tickerMap,
            ambiguousTickers,
            canonicalInstrumentCodeById,
            unresolvedCodes,
            ambiguousCodes);
    }

    private static string? NormalizeCode(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToUpperInvariant();
}

internal sealed record BrokerImportInstrumentResolution(
    IReadOnlyDictionary<string, Guid> AliasMap,
    IReadOnlyDictionary<string, Guid> IsinMap,
    IReadOnlyDictionary<string, Guid> TickerMap,
    ISet<string> AmbiguousTickers,
    IReadOnlyDictionary<Guid, string> CanonicalInstrumentCodeById,
    ISet<string> UnresolvedCodes,
    ISet<string> AmbiguousCodes)
{
    public static BrokerImportInstrumentResolution Empty { get; } =
        new(
            new Dictionary<string, Guid>(StringComparer.Ordinal),
            new Dictionary<string, Guid>(StringComparer.Ordinal),
            new Dictionary<string, Guid>(StringComparer.Ordinal),
            new HashSet<string>(StringComparer.Ordinal),
            new Dictionary<Guid, string>(),
            new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            new HashSet<string>(StringComparer.OrdinalIgnoreCase));

    public bool HasErrors => UnresolvedCodes.Count > 0 || AmbiguousCodes.Count > 0;

    public IEnumerable<string> BuildErrors() =>
        UnresolvedCodes.Select(x => $"Не найден инструмент {x}")
            .Concat(AmbiguousCodes.Select(x => $"Найдено несколько инструментов с тикером {x}. Используйте уникальный ISIN."));

    public Guid ResolveInstrumentId(string? instrumentCode)
    {
        var normalizedCode = NormalizeCode(instrumentCode)
            ?? throw new InvalidOperationException("Instrument code is required for resolvable import operation.");

        if (AliasMap.TryGetValue(normalizedCode, out var aliasId))
        {
            return aliasId;
        }

        if (IsinMap.TryGetValue(normalizedCode, out var isinId))
        {
            return isinId;
        }

        if (TickerMap.TryGetValue(normalizedCode, out var tickerId))
        {
            return tickerId;
        }

        throw new InvalidOperationException($"Instrument code '{instrumentCode}' was not resolved before batch preparation.");
    }

    private static string? NormalizeCode(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToUpperInvariant();
}
