using Larchik.Application.Helpers;
using Larchik.Application.Portfolios.Valuation;
using Larchik.Persistence.Context;
using Larchik.Persistence.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Larchik.Application.Prices.SyncPrices;

public class SyncPricesCommandHandler(LarchikContext context)
    : IRequestHandler<SyncPricesCommand, Result<int>>
{
    public async Task<Result<int>> Handle(SyncPricesCommand request, CancellationToken cancellationToken)
    {
        var instrumentIds = request.Prices.Select(p => p.InstrumentId).Distinct().ToArray();
        var knownInstruments = await context.Instruments
            .AsNoTracking()
            .Where(i => instrumentIds.Contains(i.Id))
            .ToDictionaryAsync(i => i.Id, cancellationToken);

        var filtered = request.Prices
            .Where(p => knownInstruments.ContainsKey(p.InstrumentId))
            .ToList();

        var listingHistories = await InstrumentListingHistoryResolver.LoadAsync(context, knownInstruments.Keys, cancellationToken);
        var normalizedInputs = filtered
            .Select(priceModel =>
            {
                var instrument = knownInstruments[priceModel.InstrumentId];
                var sourceCurrency = priceModel.CurrencyId.Trim().ToUpperInvariant();
                var expectedSourceCurrency = InstrumentListingHistoryResolver.ResolveCurrency(instrument, listingHistories, priceModel.Date);
                return new
                {
                    Model = priceModel,
                    Instrument = instrument,
                    SourceCurrency = sourceCurrency,
                    ExpectedSourceCurrency = expectedSourceCurrency
                };
            })
            .ToList();
        var normalizedInputsByKey = normalizedInputs.ToDictionary(
            x => (x.Model.InstrumentId, x.Model.Date.Date, Provider: x.Model.Provider.ToUpperInvariant()));

        var mismatches = normalizedInputs
            .Where(x => !string.Equals(x.SourceCurrency, x.ExpectedSourceCurrency, StringComparison.OrdinalIgnoreCase))
            .Take(5)
            .Select(x =>
                $"{x.Instrument.Ticker} {x.Model.Date:yyyy-MM-dd}: source {x.SourceCurrency}, expected {x.ExpectedSourceCurrency}")
            .ToArray();

        if (mismatches.Length > 0)
        {
            return Result<int>.Failure($"Price currency mismatch with active listing: {string.Join("; ", mismatches)}");
        }

        var neededCurrencies = normalizedInputs
            .SelectMany(x => new[] { x.SourceCurrency, x.Instrument.CurrencyId })
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var fxRates = neededCurrencies.Length == 0
            ? []
            : await context.FxRates
                .AsNoTracking()
                .Where(x => neededCurrencies.Contains(x.BaseCurrencyId) && neededCurrencies.Contains(x.QuoteCurrencyId))
                .ToListAsync(cancellationToken);
        var data = new HistoricalDataLookup([], fxRates);
        var missingRates = normalizedInputs
            .Where(x =>
                !string.Equals(x.SourceCurrency, x.Instrument.CurrencyId, StringComparison.OrdinalIgnoreCase) &&
                data.GetRate(x.SourceCurrency, x.Instrument.CurrencyId, x.Model.Date) is null)
            .Take(5)
            .Select(x =>
                $"{x.Instrument.Ticker} {x.Model.Date:yyyy-MM-dd}: {x.SourceCurrency}->{x.Instrument.CurrencyId}")
            .ToArray();

        if (missingRates.Length > 0)
        {
            return Result<int>.Failure($"FX rate is missing for price normalization: {string.Join("; ", missingRates)}");
        }

        var instrumentPriceDates = filtered
            .Select(p => (p.InstrumentId, Date: p.Date.Date, Provider: p.Provider.ToUpperInvariant()))
            .ToHashSet();

        var existing = await context.Prices
            .Where(x => instrumentIds.Contains(x.InstrumentId))
            .Where(x => instrumentPriceDates.Any(t =>
                t.Item1 == x.InstrumentId &&
                t.Item2 == x.Date.Date &&
                t.Item3 == x.Provider.ToUpper()))
            .ToListAsync(cancellationToken);

        foreach (var priceModel in filtered)
        {
            var provider = priceModel.Provider.ToUpperInvariant();
            var normalizedInput = normalizedInputsByKey[(priceModel.InstrumentId, priceModel.Date.Date, provider)];
            var existingPrice = existing.FirstOrDefault(x =>
                x.InstrumentId == priceModel.InstrumentId &&
                x.Date.Date == priceModel.Date.Date &&
                x.Provider.ToUpper() == provider);
            var normalizedValue = data.Convert(
                priceModel.Value,
                normalizedInput.SourceCurrency,
                normalizedInput.Instrument.CurrencyId,
                priceModel.Date);

            if (existingPrice is null)
            {
                await context.Prices.AddAsync(new Price
                {
                    Id = Guid.NewGuid(),
                    InstrumentId = priceModel.InstrumentId,
                    Date = priceModel.Date,
                    Value = normalizedValue,
                    CurrencyId = normalizedInput.Instrument.CurrencyId.ToUpperInvariant(),
                    SourceCurrencyId = normalizedInput.SourceCurrency,
                    Provider = provider,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                }, cancellationToken);
            }
            else
            {
                existingPrice.Value = normalizedValue;
                existingPrice.CurrencyId = normalizedInput.Instrument.CurrencyId.ToUpperInvariant();
                existingPrice.SourceCurrencyId = normalizedInput.SourceCurrency;
                existingPrice.Provider = provider;
                existingPrice.UpdatedAt = DateTime.UtcNow;
            }
        }

        var changes = await context.SaveChangesAsync(cancellationToken);
        return Result<int>.Success(changes);
    }
}
