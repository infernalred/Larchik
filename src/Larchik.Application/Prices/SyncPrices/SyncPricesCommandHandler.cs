using Larchik.Application.Helpers;
using Larchik.Application.Portfolios.Valuation;
using Larchik.Persistence.Context;
using Larchik.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace Larchik.Application.Prices.SyncPrices;

public class SyncPricesCommandHandler(LarchikContext context)
{
    public async Task<Result<int>> Handle(SyncPricesCommand request, CancellationToken cancellationToken)
    {
        var knownInstruments = await LoadKnownInstrumentsAsync(request, cancellationToken);
        if (knownInstruments.Count == 0)
        {
            return Result<int>.Success(0);
        }

        var normalizedInputs = await NormalizeInputsAsync(request, knownInstruments, cancellationToken);
        if (normalizedInputs.Count == 0)
        {
            return Result<int>.Success(0);
        }

        var mismatches = GetCurrencyMismatchErrors(normalizedInputs);
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
            : await MarketFxRateLoader.LoadAsync(context, neededCurrencies, cancellationToken);
        var data = new HistoricalDataLookup([], fxRates);
        var missingRates = GetMissingFxErrors(normalizedInputs, data);
        if (missingRates.Length > 0)
        {
            return Result<int>.Failure($"FX rate is missing for price normalization: {string.Join("; ", missingRates)}");
        }

        var upsertInputs = BuildUpsertInputs(normalizedInputs, data);

        await PriceStorageHelper.ApplyAsync(context, upsertInputs, cancellationToken);

        var changes = await context.SaveChangesAsync(cancellationToken);
        return Result<int>.Success(changes);
    }

    private async Task<Dictionary<Guid, Instrument>> LoadKnownInstrumentsAsync(
        SyncPricesCommand request,
        CancellationToken cancellationToken)
    {
        var requestedInstrumentIds = request.Prices
            .Select(x => x.InstrumentId)
            .Distinct()
            .ToArray();

        return await context.Instruments
            .Where(x => requestedInstrumentIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, cancellationToken);
    }

    private async Task<List<NormalizedPriceInput>> NormalizeInputsAsync(
        SyncPricesCommand request,
        IReadOnlyDictionary<Guid, Instrument> knownInstruments,
        CancellationToken cancellationToken)
    {
        var filteredInputs = request.Prices
            .Where(x => knownInstruments.ContainsKey(x.InstrumentId))
            .ToList();

        if (filteredInputs.Count == 0)
        {
            return [];
        }

        var listingHistories = await InstrumentListingHistoryResolver.LoadAsync(
            context,
            knownInstruments.Keys,
            cancellationToken);

        return filteredInputs
            .Select(model =>
            {
                var instrument = knownInstruments[model.InstrumentId];
                var normalizedDate = PriceStorageHelper.NormalizeUtcDate(model.Date);
                var sourceCurrency = model.CurrencyId.Trim().ToUpperInvariant();
                var provider = model.Provider.Trim().ToUpperInvariant();
                var expectedSourceCurrency = InstrumentListingHistoryResolver.ResolveCurrency(
                    instrument,
                    listingHistories,
                    normalizedDate);

                return new NormalizedPriceInput(
                    model.InstrumentId,
                    normalizedDate,
                    model.Value,
                    provider,
                    sourceCurrency,
                    expectedSourceCurrency,
                    instrument);
            })
            .ToList();
    }

    private static string[] GetCurrencyMismatchErrors(IReadOnlyCollection<NormalizedPriceInput> normalizedInputs) =>
        normalizedInputs
            .Where(x => !string.Equals(x.SourceCurrency, x.ExpectedSourceCurrency, StringComparison.OrdinalIgnoreCase))
            .Take(5)
            .Select(x => $"{x.Instrument.Ticker} {x.Date:yyyy-MM-dd}: source {x.SourceCurrency}, expected {x.ExpectedSourceCurrency}")
            .ToArray();

    private static string[] GetMissingFxErrors(
        IReadOnlyCollection<NormalizedPriceInput> normalizedInputs,
        HistoricalDataLookup data) =>
        normalizedInputs
            .Where(x =>
                !string.Equals(x.SourceCurrency, x.Instrument.CurrencyId, StringComparison.OrdinalIgnoreCase) &&
                data.GetRate(x.SourceCurrency, x.Instrument.CurrencyId, x.Date) is null)
            .Take(5)
            .Select(x => $"{x.Instrument.Ticker} {x.Date:yyyy-MM-dd}: {x.SourceCurrency}->{x.Instrument.CurrencyId}")
            .ToArray();

    private static List<PriceStorageHelper.UpsertPriceInput> BuildUpsertInputs(
        IReadOnlyCollection<NormalizedPriceInput> normalizedInputs,
        HistoricalDataLookup data) =>
        normalizedInputs
            .Select(x => new PriceStorageHelper.UpsertPriceInput(
                x.InstrumentId,
                x.Date,
                data.Convert(x.Value, x.SourceCurrency, x.Instrument.CurrencyId, x.Date),
                x.Instrument.CurrencyId,
                x.SourceCurrency,
                x.Provider))
            .ToList();

    private sealed record NormalizedPriceInput(
        Guid InstrumentId,
        DateTime Date,
        decimal Value,
        string Provider,
        string SourceCurrency,
        string ExpectedSourceCurrency,
        Instrument Instrument);
}
