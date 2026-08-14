using System.Collections.Concurrent;
using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Larchik.Application.Helpers;
using Larchik.Persistence.Context;
using Larchik.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Larchik.Application.Prices.SyncTbankPrices;

public class SyncTbankPricesCommandHandler(
    LarchikContext context,
    IHttpClientFactory httpClientFactory,
    ILogger<SyncTbankPricesCommandHandler> logger)
{
    private const string DefaultBaseUrl =
        "https://invest-public-api.tbank.ru/rest/tinkoff.public.invest.api.contract.v1.MarketDataService/GetCandles";
    private const string DefaultAccruedInterestsBaseUrl =
        "https://invest-public-api.tbank.ru/rest/tinkoff.public.invest.api.contract.v1.InstrumentsService/GetAccruedInterests";
    private static readonly string[] DefaultCountryExclusions = [];

    public async Task<Result<int>> Handle(SyncTbankPricesCommand request, CancellationToken cancellationToken)
    {
        var date = request.Date ?? DateOnly.FromDateTime(DateTime.UtcNow.Date);
        var provider = string.IsNullOrWhiteSpace(request.Provider) ? "TBANK" : request.Provider.Trim().ToUpperInvariant();
        var baseUrl = string.IsNullOrWhiteSpace(request.BaseUrl) ? DefaultBaseUrl : request.BaseUrl.Trim();
        var accruedInterestsBaseUrl = string.IsNullOrWhiteSpace(request.AccruedInterestsBaseUrl)
            ? DefaultAccruedInterestsBaseUrl
            : request.AccruedInterestsBaseUrl.Trim();
        var token = request.Token?.Trim();
        var allowInvalidTls = request.AllowInvalidTls ?? false;
        var lookbackDays = Math.Clamp(request.MaxHistoryLookbackDays ?? 7, 1, 31);
        var maxParallelism = Math.Clamp(request.MaxParallelism ?? 6, 1, 32);
        var excludedCountries = (request.CountryExclusions ?? DefaultCountryExclusions)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim().ToUpperInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(token))
        {
            return Result<int>.Failure("TBANK token is not configured");
        }

        logger.LogInformation(
            "TBANK price sync started for {Date} UTC. Provider: {Provider}. Lookback days: {LookbackDays}. " +
            "Excluded countries: {ExcludedCountries}",
            date.ToString("yyyy-MM-dd"),
            provider,
            lookbackDays,
            excludedCountries.Count == 0 ? "none" : string.Join(",", excludedCountries));

        var instrumentLoad = await LoadEligibleInstrumentsAsync(date, excludedCountries, cancellationToken);
        var instrumentStates = instrumentLoad.States;
        var listingHistories = instrumentLoad.ListingHistories;
        var instruments = instrumentLoad.Candidates;

        if (instruments.Count == 0)
        {
            logger.LogInformation("TBANK price sync skipped for {Date} UTC: no eligible instruments found", date.ToString("yyyy-MM-dd"));
            return Result<int>.Success(0);
        }

        using var client = CreateClient(allowInvalidTls);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var loadResult = await LoadPricePointsAsync(
            client,
            instruments,
            date,
            lookbackDays,
            maxParallelism,
            baseUrl,
            accruedInterestsBaseUrl,
            cancellationToken);

        if (loadResult.Points.Count == 0)
        {
            var errorMessage = loadResult.Errors.Count == 0
                ? $"TBANK returned no prices for {date:yyyy-MM-dd}"
                : string.Join("; ", loadResult.Errors.Take(10));
            return Result<int>.Failure(errorMessage);
        }

        var points = loadResult.Points;
        var sourceDates = points
            .Select(x => x.Date)
            .Distinct()
            .OrderBy(x => x)
            .ToArray();

        var upsertInputs = BuildUpsertInputs(points, instrumentStates, listingHistories, provider);
        var upsertResult = await PriceStorageHelper.ApplyAsync(context, upsertInputs, cancellationToken);
        var changes = await context.SaveChangesAsync(cancellationToken);
        var sourceDateDistribution = points
            .GroupBy(x => x.Date)
            .OrderBy(x => x.Key)
            .Select(x => $"{x.Key:yyyy-MM-dd}:{x.Count()}")
            .ToArray();

        logger.LogInformation(
            "TBANK price sync finished for {Date} UTC. Eligible instruments: {Eligible}. Loaded: {Loaded}. " +
            "Missing candles: {Missing}. Errors: {Errors}. Source dates: {SourceDates}. Inserted: {Inserted}, updated: {Updated}, db changes: {Changes}",
            date.ToString("yyyy-MM-dd"),
            instruments.Count,
            points.Count,
            loadResult.MissingCount,
            loadResult.Errors.Count,
            sourceDateDistribution.Length == 0 ? "none" : string.Join(", ", sourceDateDistribution),
            upsertResult.Inserted,
            upsertResult.Updated,
            changes);

        if (loadResult.Errors.Count > 0)
        {
            logger.LogWarning(
                "TBANK price sync had {ErrorCount} request errors for {Date} UTC. Sample: {Sample}",
                loadResult.Errors.Count,
                date.ToString("yyyy-MM-dd"),
                string.Join("; ", loadResult.Errors.Take(5)));
        }

        return Result<int>.Success(changes);
    }

    private async Task<TbankInstrumentLoadResult> LoadEligibleInstrumentsAsync(
        DateOnly date,
        ISet<string> excludedCountries,
        CancellationToken cancellationToken)
    {
        var positionDate = ToUtcDateTime(date).Date.AddDays(1).AddTicks(-1);
        var openInstrumentIds = await LoadOpenPositionInstrumentIdsAsync(positionDate, cancellationToken);
        if (openInstrumentIds.Count == 0)
        {
            return new TbankInstrumentLoadResult([], [], new Dictionary<Guid, IReadOnlyList<InstrumentListingHistory>>());
        }

        var instrumentsQuery = context.Instruments
            .Where(x =>
                openInstrumentIds.Contains(x.Id) &&
                (x.Type == InstrumentType.Equity || x.Type == InstrumentType.Bond || x.Type == InstrumentType.Etf || x.Type == InstrumentType.Currency) &&
                x.IsTrading &&
                x.PriceSource == Persistence.Entities.PriceSource.TBANK &&
                x.Figi != null &&
                x.Figi != "");

        if (excludedCountries.Count > 0)
        {
            instrumentsQuery = instrumentsQuery.Where(x => x.CountryId == null || !excludedCountries.Contains(x.CountryId.ToUpper()));
        }

        var instrumentStates = await instrumentsQuery
            .Select(x => new InstrumentState(x.Id, x.Figi!, x.CurrencyId.ToUpperInvariant(), x.Ticker, x.Isin, x.ExchangeId, x.Type))
            .ToListAsync(cancellationToken);

        var listingHistories = await InstrumentListingHistoryResolver.LoadAsync(
            context,
            instrumentStates.Select(x => x.Id),
            cancellationToken);

        var candidates = instrumentStates
            .Select(x =>
            {
                var activeListing = InstrumentListingHistoryResolver.Resolve(
                    x.Id,
                    x.Ticker,
                    x.Figi,
                    x.Exchange,
                    x.CurrencyId,
                    listingHistories,
                    date.ToDateTime(TimeOnly.MinValue));
                var figi = string.IsNullOrWhiteSpace(activeListing.Figi) ? x.Figi : activeListing.Figi!;
                return new InstrumentCandidate(x.Id, figi, x.CurrencyId, x.Ticker, x.Isin, x.Exchange, x.Type);
            })
            .ToList();

        return new TbankInstrumentLoadResult(instrumentStates, candidates, listingHistories);
    }

    private async Task<HashSet<Guid>> LoadOpenPositionInstrumentIdsAsync(DateTime asOfDate, CancellationToken cancellationToken)
    {
        var positionDeltas = await context.Operations
            .Where(x => x.InstrumentId != null && x.TradeDate <= asOfDate)
            .Where(x =>
                x.Type == OperationType.Buy ||
                x.Type == OperationType.Sell ||
                x.Type == OperationType.TransferIn ||
                x.Type == OperationType.TransferOut ||
                x.Type == OperationType.BondMaturity)
            .GroupBy(x => x.InstrumentId!.Value)
            .Select(x => new
            {
                InstrumentId = x.Key,
                Quantity = x.Sum(operation =>
                    operation.Type == OperationType.Buy ||
                    operation.Type == OperationType.TransferIn
                        ? operation.Quantity
                        : -operation.Quantity)
            })
            .Where(x => x.Quantity != 0)
            .ToListAsync(cancellationToken);

        return positionDeltas
            .Select(x => x.InstrumentId)
            .ToHashSet();
    }

    private async Task<TbankPointLoadResult> LoadPricePointsAsync(
        HttpClient client,
        IReadOnlyCollection<InstrumentCandidate> instruments,
        DateOnly date,
        int lookbackDays,
        int maxParallelism,
        string baseUrl,
        string accruedInterestsBaseUrl,
        CancellationToken cancellationToken)
    {
        var loadedPoints = new ConcurrentBag<TbankPricePoint>();
        var errors = new ConcurrentBag<string>();
        var missing = 0;
        var semaphore = new SemaphoreSlim(maxParallelism);

        await Task.WhenAll(instruments.Select(async instrument =>
        {
            await semaphore.WaitAsync(cancellationToken);
            try
            {
                var pointResult = await LoadPricePoint(
                    client,
                    instrument,
                    date,
                    lookbackDays,
                    baseUrl,
                    accruedInterestsBaseUrl,
                    cancellationToken);
                if (pointResult.IsSuccess)
                {
                    if (pointResult.Value is null)
                    {
                        Interlocked.Increment(ref missing);
                    }
                    else
                    {
                        loadedPoints.Add(pointResult.Value);
                    }
                }
                else
                {
                    errors.Add(pointResult.Error ?? $"TBANK request failed for {instrument.Ticker}");
                }
            }
            finally
            {
                semaphore.Release();
            }
        }));

        return new TbankPointLoadResult(loadedPoints.ToList(), missing, errors.ToList());
    }

    private static List<PriceStorageHelper.UpsertPriceInput> BuildUpsertInputs(
        IReadOnlyCollection<TbankPricePoint> points,
        IReadOnlyCollection<InstrumentState> instrumentStates,
        IReadOnlyDictionary<Guid, IReadOnlyList<InstrumentListingHistory>> listingHistories,
        string provider)
    {
        var instrumentStateById = instrumentStates.ToDictionary(x => x.Id);

        return points
            .Where(point => instrumentStateById.ContainsKey(point.InstrumentId))
            .Select(point =>
            {
                var instrumentState = instrumentStateById[point.InstrumentId];
                var activeListing = InstrumentListingHistoryResolver.Resolve(
                    instrumentState.Id,
                    instrumentState.Ticker,
                    instrumentState.Figi,
                    instrumentState.Exchange,
                    instrumentState.CurrencyId,
                    listingHistories,
                    ToUtcDateTime(point.Date));

                return new PriceStorageHelper.UpsertPriceInput(
                    point.InstrumentId,
                    ToUtcDateTime(point.Date),
                    point.Value,
                    instrumentState.CurrencyId,
                    activeListing.CurrencyId,
                    provider);
            })
            .ToList();
    }

    private HttpClient CreateClient(bool allowInvalidTls)
    {
        if (!allowInvalidTls)
        {
            return httpClientFactory.CreateClient();
        }

        return new HttpClient(new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
        });
    }

    private async Task<Result<TbankPricePoint?>> LoadPricePoint(
        HttpClient client,
        InstrumentCandidate instrument,
        DateOnly requestedDate,
        int lookbackDays,
        string baseUrl,
        string accruedInterestsBaseUrl,
        CancellationToken cancellationToken)
    {
        try
        {
            var fromDate = requestedDate.AddDays(-(lookbackDays - 1));
            var payload = JsonSerializer.Serialize(new
            {
                from = $"{fromDate:yyyy-MM-dd}T00:00:00Z",
                to = $"{requestedDate:yyyy-MM-dd}T23:59:59Z",
                interval = "CANDLE_INTERVAL_DAY",
                instrumentId = instrument.Figi
            });

            using var response = await client.PostAsync(
                baseUrl,
                new StringContent(payload, Encoding.UTF8, "application/json"),
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var body = await HttpContentReader.ReadAsStringSafeAsync(response.Content, cancellationToken);
                return Result<TbankPricePoint?>.Failure(
                    $"TBANK request failed for {instrument.Ticker}/{instrument.Isin}: {(int)response.StatusCode} {TrimBody(body)}");
            }

            var json = await HttpContentReader.ReadAsStringSafeAsync(response.Content, cancellationToken);
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("candles", out var candlesElement) ||
                candlesElement.ValueKind != JsonValueKind.Array)
            {
                return Result<TbankPricePoint?>.Success(null);
            }

            TbankPricePoint? best = null;
            foreach (var candleElement in candlesElement.EnumerateArray())
            {
                if (!TryParseDate(candleElement, out var candleDate) || candleDate > requestedDate)
                {
                    continue;
                }

                if (!TryParseClosePrice(candleElement, out var price) || price <= 0)
                {
                    continue;
                }

                var point = new TbankPricePoint(instrument.Id, candleDate, price, instrument.CurrencyId, instrument.Ticker, instrument.Isin);
                if (best is null || point.Date > best.Date)
                {
                    best = point;
                }
            }

            if (best is null || instrument.Type != InstrumentType.Bond)
            {
                return Result<TbankPricePoint?>.Success(best);
            }

            var accruedResult = await LoadBondAccruedInterest(
                client,
                instrument,
                best.Date,
                accruedInterestsBaseUrl,
                cancellationToken);
            if (!accruedResult.IsSuccess || accruedResult.Value is null)
            {
                return Result<TbankPricePoint?>.Failure(
                    accruedResult.Error ?? $"TBANK returned no accrued interest for bond {instrument.Ticker} on {best.Date:yyyy-MM-dd}");
            }

            var accrued = accruedResult.Value;
            var dirtyPrice = best.Value / 100m * accrued.Nominal + accrued.Value;
            return Result<TbankPricePoint?>.Success(best with { Value = dirtyPrice });
        }
        catch (Exception ex)
        {
            return Result<TbankPricePoint?>.Failure(
                $"TBANK request failed for {instrument.Ticker}/{instrument.Isin}: {ex.Message}");
        }
    }

    private static async Task<Result<BondAccruedInterest?>> LoadBondAccruedInterest(
        HttpClient client,
        InstrumentCandidate instrument,
        DateOnly date,
        string baseUrl,
        CancellationToken cancellationToken)
    {
        try
        {
            var payload = JsonSerializer.Serialize(new
            {
                from = $"{date:yyyy-MM-dd}T00:00:00Z",
                to = $"{date:yyyy-MM-dd}T23:59:59Z",
                instrumentId = instrument.Figi
            });
            using var response = await client.PostAsync(
                baseUrl,
                new StringContent(payload, Encoding.UTF8, "application/json"),
                cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var body = await HttpContentReader.ReadAsStringSafeAsync(response.Content, cancellationToken);
                return Result<BondAccruedInterest?>.Failure(
                    $"TBANK accrued-interest request failed for {instrument.Ticker}/{instrument.Isin}: {(int)response.StatusCode} {TrimBody(body)}");
            }

            var json = await HttpContentReader.ReadAsStringSafeAsync(response.Content, cancellationToken);
            using var doc = JsonDocument.Parse(json);
            var hasItems = TryGetProperty(doc.RootElement, "accruedInterests", out var items) ||
                           TryGetProperty(doc.RootElement, "accrued_interests", out items);
            if (!hasItems || items.ValueKind != JsonValueKind.Array)
            {
                return Result<BondAccruedInterest?>.Success(null);
            }

            BondAccruedInterest? best = null;
            foreach (var item in items.EnumerateArray())
            {
                if (!TryGetDate(item, out var itemDate) || itemDate > date ||
                    !TryGetProperty(item, "nominal", out var nominalElement) ||
                    !TryParseQuotation(nominalElement, out var nominal) || nominal <= 0m ||
                    !TryGetProperty(item, "value", out var valueElement) ||
                    !TryParseQuotation(valueElement, out var accruedValue))
                {
                    continue;
                }

                if (best is null || itemDate > best.Date)
                {
                    best = new BondAccruedInterest(itemDate, nominal, accruedValue);
                }
            }

            return Result<BondAccruedInterest?>.Success(best);
        }
        catch (Exception ex)
        {
            return Result<BondAccruedInterest?>.Failure(
                $"TBANK accrued-interest request failed for {instrument.Ticker}/{instrument.Isin}: {ex.Message}");
        }
    }

    private static bool TryParseDate(JsonElement candleElement, out DateOnly date)
    {
        date = default;
        if (!candleElement.TryGetProperty("time", out var timeElement))
        {
            return false;
        }

        var value = timeElement.GetString();
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        if (!DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed))
        {
            return false;
        }

        date = DateOnly.FromDateTime(parsed.UtcDateTime.Date);
        return true;
    }

    private static bool TryParseClosePrice(JsonElement candleElement, out decimal price)
    {
        price = 0;
        if (!candleElement.TryGetProperty("close", out var closeElement) ||
            closeElement.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        return TryParseQuotation(closeElement, out price);
    }

    private static bool TryParseQuotation(JsonElement element, out decimal value)
    {
        value = 0m;
        if (!TryParseDecimal(element, "units", out var units))
        {
            units = 0;
        }

        if (!TryParseDecimal(element, "nano", out var nano))
        {
            nano = 0;
        }

        value = units + nano / 1_000_000_000m;
        return true;
    }

    private static bool TryGetProperty(JsonElement element, string propertyName, out JsonElement value)
    {
        if (element.TryGetProperty(propertyName, out value))
        {
            return true;
        }

        foreach (var property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }

    private static bool TryGetDate(JsonElement element, out DateOnly date)
    {
        date = default;
        if (!TryGetProperty(element, "date", out var dateElement) ||
            !DateTimeOffset.TryParse(dateElement.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed))
        {
            return false;
        }

        date = DateOnly.FromDateTime(parsed.UtcDateTime);
        return true;
    }

    private static bool TryParseDecimal(JsonElement element, string propertyName, out decimal value)
    {
        value = 0;
        if (!element.TryGetProperty(propertyName, out var property))
        {
            return false;
        }

        switch (property.ValueKind)
        {
            case JsonValueKind.Number when property.TryGetDecimal(out value):
                return true;
            case JsonValueKind.Number when property.TryGetInt64(out var int64):
                value = int64;
                return true;
            case JsonValueKind.String:
                return decimal.TryParse(property.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out value);
            default:
                return false;
        }
    }

    private static string TrimBody(string? body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return "empty response";
        }

        var normalized = body.Replace(Environment.NewLine, " ").Trim();
        return normalized.Length <= 180 ? normalized : normalized[..180];
    }

    private static DateTime ToUtcDateTime(DateOnly date)
    {
        return DateTime.SpecifyKind(date.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
    }

    private sealed record TbankInstrumentLoadResult(
        List<InstrumentState> States,
        List<InstrumentCandidate> Candidates,
        IReadOnlyDictionary<Guid, IReadOnlyList<InstrumentListingHistory>> ListingHistories);

    private sealed record TbankPointLoadResult(
        List<TbankPricePoint> Points,
        int MissingCount,
        List<string> Errors);

    private sealed record InstrumentCandidate(Guid Id, string Figi, string CurrencyId, string Ticker, string? Isin, string? Exchange, InstrumentType Type);
    private sealed record InstrumentState(Guid Id, string Figi, string CurrencyId, string Ticker, string? Isin, string? Exchange, InstrumentType Type);
    private sealed record TbankPricePoint(Guid InstrumentId, DateOnly Date, decimal Value, string CurrencyId, string Ticker, string? Isin);
    private sealed record BondAccruedInterest(DateOnly Date, decimal Nominal, decimal Value);
}
