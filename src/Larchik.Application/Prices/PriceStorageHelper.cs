using Larchik.Persistence.Context;
using Larchik.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace Larchik.Application.Prices;

internal static class PriceStorageHelper
{
    public static async Task<UpsertResult> ApplyAsync(
        LarchikContext context,
        IReadOnlyCollection<UpsertPriceInput> inputs,
        CancellationToken cancellationToken)
    {
        if (inputs.Count == 0)
        {
            return new UpsertResult(0, 0);
        }

        var normalizedInputs = inputs
            .Select(input => new NormalizedPriceInput(
                input.InstrumentId,
                NormalizeUtcDate(input.Date),
                input.Value,
                input.CurrencyId.Trim().ToUpperInvariant(),
                string.IsNullOrWhiteSpace(input.SourceCurrencyId) ? null : input.SourceCurrencyId.Trim().ToUpperInvariant(),
                input.Provider.Trim().ToUpperInvariant()))
            .ToList();

        var keys = normalizedInputs
            .Select(x => new PriceKey(x.InstrumentId, x.Date, x.Provider))
            .ToHashSet();
        var instrumentIds = normalizedInputs
            .Select(x => x.InstrumentId)
            .Distinct()
            .ToArray();
        var minDate = normalizedInputs.Min(x => x.Date);
        var maxDateExclusive = normalizedInputs.Max(x => x.Date).AddDays(1);

        var existing = await context.Prices
            .AsTracking()
            .Where(x => instrumentIds.Contains(x.InstrumentId))
            .Where(x => x.Date >= minDate && x.Date < maxDateExclusive)
            .ToListAsync(cancellationToken);
        var existingByKey = existing
            .Select(x => new
            {
                Price = x,
                Key = new PriceKey(x.InstrumentId, NormalizeUtcDate(x.Date), x.Provider.ToUpperInvariant())
            })
            .Where(x => keys.Contains(x.Key))
            .GroupBy(x => x.Key)
            .ToDictionary(x => x.Key, x => x.First().Price);

        var inserted = 0;
        var updated = 0;
        var now = DateTime.UtcNow;

        foreach (var input in normalizedInputs)
        {
            var key = new PriceKey(input.InstrumentId, input.Date, input.Provider);
            if (existingByKey.TryGetValue(key, out var existingPrice))
            {
                existingPrice.Date = input.Date;
                existingPrice.Value = input.Value;
                existingPrice.CurrencyId = input.CurrencyId;
                existingPrice.SourceCurrencyId = input.SourceCurrencyId;
                existingPrice.Provider = input.Provider;
                existingPrice.UpdatedAt = now;
                updated++;
                continue;
            }

            await context.Prices.AddAsync(new Price
            {
                Id = Guid.NewGuid(),
                InstrumentId = input.InstrumentId,
                Date = input.Date,
                Value = input.Value,
                CurrencyId = input.CurrencyId,
                SourceCurrencyId = input.SourceCurrencyId,
                Provider = input.Provider,
                CreatedAt = now,
                UpdatedAt = now
            }, cancellationToken);
            inserted++;
        }

        return new UpsertResult(inserted, updated);
    }

    public static DateTime NormalizeUtcDate(DateTime value)
    {
        var utcValue = value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };

        return DateTime.SpecifyKind(utcValue.Date, DateTimeKind.Utc);
    }

    internal sealed record UpsertPriceInput(
        Guid InstrumentId,
        DateTime Date,
        decimal Value,
        string CurrencyId,
        string? SourceCurrencyId,
        string Provider);

    internal sealed record UpsertResult(int Inserted, int Updated);

    private sealed record NormalizedPriceInput(
        Guid InstrumentId,
        DateTime Date,
        decimal Value,
        string CurrencyId,
        string? SourceCurrencyId,
        string Provider);

    private readonly record struct PriceKey(Guid InstrumentId, DateTime Date, string Provider);
}
