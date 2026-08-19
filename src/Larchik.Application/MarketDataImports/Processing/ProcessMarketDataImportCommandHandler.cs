using System.Text.Json;
using Larchik.Application.Prices;
using Larchik.Persistence.Context;
using Larchik.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Larchik.Application.MarketDataImports.Processing;

public sealed class ProcessMarketDataImportCommandHandler(
    LarchikContext context,
    IEnumerable<IMarketDataImportSource> sources,
    IOptions<MarketDataImportOptions> options,
    ILogger<ProcessMarketDataImportCommandHandler> logger)
{
    private readonly IReadOnlyDictionary<PriceSource, IMarketDataImportSource> sourcesByType =
        sources.ToDictionary(x => x.Source);

    public async Task<MarketDataImportProcessResult> Handle(Guid requestId, CancellationToken cancellationToken)
    {
        var request = await context.MarketDataImportRequests
            .AsTracking()
            .FirstOrDefaultAsync(x => x.Id == requestId, cancellationToken);

        if (request is null)
        {
            return MarketDataImportProcessResult.Failed($"Market data import request '{requestId}' was not found.");
        }

        if (request.Status is MarketDataImportStatus.Succeeded or
            MarketDataImportStatus.SkippedExisting or
            MarketDataImportStatus.Failed)
        {
            return MarketDataImportProcessResult.Completed();
        }

        if (!sourcesByType.TryGetValue(request.Source, out var source))
        {
            return await FailAsync(request, $"Market data source '{request.Source}' is not registered.", cancellationToken);
        }

        var now = DateTime.UtcNow;
        request.StartedAt ??= now;
        request.UpdatedAt = now;

        Instrument? instrument;
        if (!request.InstrumentId.HasValue)
        {
            instrument = await context.Instruments
                .AsTracking()
                .FirstOrDefaultAsync(x => x.Isin != null && x.Isin.ToUpper() == request.Isin, cancellationToken);
            if (instrument is not null)
            {
                request.InstrumentId = instrument.Id;
                request.Status = MarketDataImportStatus.SkippedExisting;
                request.CompletedAt = now;
                request.LastError = null;
                await context.SaveChangesAsync(cancellationToken);
                return MarketDataImportProcessResult.Completed();
            }

            request.Status = MarketDataImportStatus.ResolvingInstrument;
            var resolvedResult = await source.ResolveAsync(request.Isin, cancellationToken);
            if (!resolvedResult.IsSuccess || resolvedResult.Value is null)
            {
                return await HandleSourceFailureAsync(
                    request,
                    resolvedResult.Error ?? $"{request.Source} did not resolve {request.Isin}.",
                    resolvedResult.IsTransient,
                    cancellationToken);
            }

            var createResult = await CreateInstrumentAsync(request, resolvedResult.Value, now, cancellationToken);
            if (!createResult.IsSuccess || createResult.Instrument is null)
            {
                return await FailAsync(request, createResult.Error ?? "Instrument creation failed.", cancellationToken);
            }

            instrument = createResult.Instrument;
        }
        else
        {
            instrument = await context.Instruments
                .AsTracking()
                .FirstOrDefaultAsync(x => x.Id == request.InstrumentId.Value, cancellationToken);
            if (instrument is null)
            {
                return await FailAsync(request, "The resolved instrument no longer exists.", cancellationToken);
            }
        }

        request.Status = MarketDataImportStatus.LoadingPrices;
        var chunkDays = Math.Clamp(options.Value.ChunkDays, 1, 366);
        var chunkFrom = DateOnly.FromDateTime(request.NextDate);
        var requestedTo = DateOnly.FromDateTime(request.ToDate);
        var chunkTo = Min(chunkFrom.AddDays(chunkDays - 1), requestedTo);
        var sourceCode = request.SourceInstrumentCode ?? instrument.Figi ?? instrument.Ticker;
        var priceResult = await source.LoadPricesAsync(
            new MarketDataImportPriceLoadRequest(
                instrument.Id,
                request.Isin,
                instrument.Ticker,
                instrument.Figi,
                instrument.Type,
                instrument.CurrencyId,
                sourceCode,
                request.SourceBoard,
                request.SourceEngine,
                request.SourceMarket,
                chunkFrom,
                chunkTo),
            cancellationToken);

        if (!priceResult.IsSuccess || priceResult.Value is null)
        {
            return await HandleSourceFailureAsync(
                request,
                priceResult.Error ?? $"{request.Source} price load failed for {request.Isin}.",
                priceResult.IsTransient,
                cancellationToken);
        }

        var upsertResult = await PriceStorageHelper.ApplyAsync(
            context,
            priceResult.Value.Select(point => new PriceStorageHelper.UpsertPriceInput(
                instrument.Id,
                ToUtcDate(point.Date),
                point.Value,
                point.CurrencyId,
                point.SourceCurrencyId,
                request.Source.ToString())).ToList(),
            cancellationToken);

        request.InsertedPrices += upsertResult.Inserted;
        request.UpdatedPrices += upsertResult.Updated;
        request.Attempt = 0;
        request.LastError = null;
        request.NextDate = ToUtcDate(chunkTo.AddDays(1));
        request.UpdatedAt = DateTime.UtcNow;

        if (chunkTo >= requestedTo)
        {
            request.Status = MarketDataImportStatus.Succeeded;
            request.CompletedAt = request.UpdatedAt;
            await context.SaveChangesAsync(cancellationToken);
            logger.LogInformation(
                "Market data import {RequestId} succeeded for {Source}/{Isin}. Inserted: {Inserted}, updated: {Updated}",
                request.Id,
                request.Source,
                request.Isin,
                request.InsertedPrices,
                request.UpdatedPrices);
            return MarketDataImportProcessResult.Completed();
        }

        context.OutboxMessages.Add(CreateOutboxMessage(request.Id, request.UpdatedAt));
        await context.SaveChangesAsync(cancellationToken);
        return MarketDataImportProcessResult.Continue();
    }

    private async Task<(bool IsSuccess, Instrument? Instrument, string? Error)> CreateInstrumentAsync(
        MarketDataImportRequest request,
        ResolvedMarketDataInstrument resolved,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var currencyId = resolved.CurrencyId.Trim().ToUpperInvariant();
        if (!await context.Currencies.AnyAsync(x => x.Id == currencyId, cancellationToken))
        {
            return (false, null, $"Currency '{currencyId}' is not configured.");
        }

        var exchangeId = NormalizeOptional(resolved.ExchangeId);
        if (exchangeId is not null &&
            !await context.Exchanges.AnyAsync(x => x.Id == exchangeId, cancellationToken))
        {
            return (false, null, $"Exchange '{exchangeId}' is not configured.");
        }

        var countryId = NormalizeOptional(resolved.CountryId);
        if (countryId is not null &&
            !await context.Countries.AnyAsync(x => x.Id == countryId, cancellationToken))
        {
            countryId = null;
        }

        var preferredCategoryId = resolved.Type == InstrumentType.Etf
            ? options.Value.EtfCategoryId
            : options.Value.DefaultCategoryId;
        var categoryId = await context.Categories.AnyAsync(x => x.Id == preferredCategoryId, cancellationToken)
            ? preferredCategoryId
            : await context.Categories.OrderBy(x => x.Id).Select(x => x.Id).FirstAsync(cancellationToken);

        var instrument = new Instrument
        {
            Id = Guid.NewGuid(),
            Name = resolved.Name.Trim(),
            Ticker = resolved.Ticker.Trim().ToUpperInvariant(),
            Isin = request.Isin,
            Figi = NormalizeOptional(resolved.Figi),
            Type = resolved.Type,
            CurrencyId = currencyId,
            CategoryId = categoryId,
            ExchangeId = exchangeId,
            CountryId = countryId,
            IsTrading = resolved.IsTrading,
            PriceSource = request.Source,
            CreatedBy = request.RequestedBy,
            UpdatedBy = request.RequestedBy,
            CreatedAt = now,
            UpdatedAt = now
        };
        context.Instruments.Add(instrument);
        context.InstrumentListingHistories.Add(new InstrumentListingHistory
        {
            Id = Guid.NewGuid(),
            InstrumentId = instrument.Id,
            Ticker = instrument.Ticker,
            Figi = instrument.Figi,
            CurrencyId = instrument.CurrencyId,
            ExchangeId = instrument.ExchangeId,
            EffectiveFrom = ToUtcDate(resolved.ListedFrom ?? DateOnly.FromDateTime(request.FromDate)),
            CreatedAt = now,
            UpdatedAt = now
        });

        request.InstrumentId = instrument.Id;
        request.SourceInstrumentCode = resolved.SourceInstrumentCode.Trim().ToUpperInvariant();
        request.SourceBoard = NormalizeOptional(resolved.Board);
        request.SourceEngine = NormalizeOptional(resolved.Engine)?.ToLowerInvariant();
        request.SourceMarket = NormalizeOptional(resolved.Market)?.ToLowerInvariant();
        return (true, instrument, null);
    }

    private async Task<MarketDataImportProcessResult> HandleSourceFailureAsync(
        MarketDataImportRequest request,
        string error,
        bool isTransient,
        CancellationToken cancellationToken)
    {
        request.Attempt += 1;
        request.LastError = TrimError(error);
        request.UpdatedAt = DateTime.UtcNow;

        if (isTransient && request.Attempt < Math.Max(1, options.Value.MaxAttempts))
        {
            await context.SaveChangesAsync(cancellationToken);
            return MarketDataImportProcessResult.Retry(request.LastError);
        }

        return await FailAsync(request, request.LastError, cancellationToken);
    }

    private async Task<MarketDataImportProcessResult> FailAsync(
        MarketDataImportRequest request,
        string error,
        CancellationToken cancellationToken)
    {
        request.Status = MarketDataImportStatus.Failed;
        request.LastError = TrimError(error);
        request.CompletedAt = DateTime.UtcNow;
        request.UpdatedAt = request.CompletedAt.Value;
        await context.SaveChangesAsync(cancellationToken);
        return MarketDataImportProcessResult.Failed(request.LastError);
    }

    private static OutboxMessage CreateOutboxMessage(Guid requestId, DateTime now) => new()
    {
        Id = Guid.NewGuid(),
        MessageType = MarketDataImportMessage.MessageType,
        PayloadJson = JsonSerializer.Serialize(MarketDataImportMessage.Create(requestId)),
        OccurredAt = now,
        AvailableAt = now,
        CreatedAt = now,
        UpdatedAt = now
    };

    private static DateOnly Min(DateOnly left, DateOnly right) => left <= right ? left : right;
    private static DateTime ToUtcDate(DateOnly date) =>
        DateTime.SpecifyKind(date.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToUpperInvariant();
    private static string TrimError(string error) => error.Length <= 4000 ? error : error[..4000];
}
