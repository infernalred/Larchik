using System.Collections.Concurrent;
using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Larchik.Application.Helpers;
using Larchik.Persistence.Context;
using Larchik.Persistence.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Larchik.Application.Prices.SyncTbankPrices;

public class SyncTbankPricesCommandHandler(
    LarchikContext context,
    IHttpClientFactory httpClientFactory,
    ILogger<SyncTbankPricesCommandHandler> logger)
    : IRequestHandler<SyncTbankPricesCommand, Result<int>>
{
    private const string DefaultBaseUrl =
        "https://invest-public-api.tbank.ru/rest/tinkoff.public.invest.api.contract.v1.MarketDataService/GetCandles";
    private static readonly string[] DefaultCountryExclusions = ["RU"];

    public async Task<Result<int>> Handle(SyncTbankPricesCommand request, CancellationToken cancellationToken)
    {
        var date = request.Date ?? DateOnly.FromDateTime(DateTime.UtcNow.Date);
        var provider = string.IsNullOrWhiteSpace(request.Provider) ? "TBANK" : request.Provider.Trim().ToUpperInvariant();
        var baseUrl = string.IsNullOrWhiteSpace(request.BaseUrl) ? DefaultBaseUrl : request.BaseUrl.Trim();
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

        var instrumentsQuery = context.Instruments
            .AsNoTracking()
            .Where(x =>
                (x.Type == InstrumentType.Equity || x.Type == InstrumentType.Etf || x.Type == InstrumentType.Currency) &&
                x.Figi != null &&
                x.Figi != "");

        if (excludedCountries.Count > 0)
        {
            instrumentsQuery = instrumentsQuery.Where(x => x.Country == null || !excludedCountries.Contains(x.Country.ToUpper()));
        }

        var instruments = await instrumentsQuery
            .Select(x => new InstrumentCandidate(x.Id, x.Figi!, x.CurrencyId.ToUpperInvariant(), x.Ticker, x.Isin))
            .ToListAsync(cancellationToken);

        if (instruments.Count == 0)
        {
            logger.LogInformation("TBANK price sync skipped for {Date} UTC: no eligible instruments found", date.ToString("yyyy-MM-dd"));
            return Result<int>.Success(0);
        }

        using var client = CreateClient(allowInvalidTls);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var loadedPoints = new ConcurrentBag<TbankPricePoint>();
        var errors = new ConcurrentBag<string>();
        var missing = 0;
        var semaphore = new SemaphoreSlim(maxParallelism);

        await Task.WhenAll(instruments.Select(async instrument =>
        {
            await semaphore.WaitAsync(cancellationToken);
            try
            {
                var pointResult = await LoadPricePoint(client, instrument, date, lookbackDays, baseUrl, cancellationToken);
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

        if (loadedPoints.IsEmpty)
        {
            var errorMessage = errors.IsEmpty
                ? $"TBANK returned no prices for {date:yyyy-MM-dd}"
                : string.Join("; ", errors.Take(10));
            return Result<int>.Failure(errorMessage);
        }

        var points = loadedPoints.ToList();
        var instrumentIds = points.Select(x => x.InstrumentId).Distinct().ToArray();
        var sourceDateValues = points
            .Select(x => x.Date.ToDateTime(TimeOnly.MinValue).Date)
            .Distinct()
            .ToArray();

        var existing = await context.Prices
            .Where(x => instrumentIds.Contains(x.InstrumentId))
            .Where(x => sourceDateValues.Contains(x.Date.Date))
            .Where(x => x.Provider.ToUpper() == provider)
            .ToListAsync(cancellationToken);

        var existingByInstrumentDate = existing
            .OrderByDescending(x => x.UpdatedAt)
            .GroupBy(x => new { x.InstrumentId, Date = DateOnly.FromDateTime(x.Date) })
            .ToDictionary(x => x.Key, x => x.First());

        var inserted = 0;
        var updated = 0;
        var now = DateTime.UtcNow;

        foreach (var point in points)
        {
            var existingKey = new { point.InstrumentId, point.Date };
            if (existingByInstrumentDate.TryGetValue(existingKey, out var price))
            {
                price.Value = point.Value;
                price.CurrencyId = point.CurrencyId;
                price.Provider = provider;
                price.UpdatedAt = now;
                updated++;
                continue;
            }

            await context.Prices.AddAsync(new Price
            {
                Id = Guid.NewGuid(),
                InstrumentId = point.InstrumentId,
                Date = point.Date.ToDateTime(TimeOnly.MinValue),
                Value = point.Value,
                CurrencyId = point.CurrencyId,
                Provider = provider,
                CreatedAt = now,
                UpdatedAt = now
            }, cancellationToken);
            inserted++;
        }

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
            missing,
            errors.Count,
            sourceDateDistribution.Length == 0 ? "none" : string.Join(", ", sourceDateDistribution),
            inserted,
            updated,
            changes);

        if (!errors.IsEmpty)
        {
            logger.LogWarning(
                "TBANK price sync had {ErrorCount} request errors for {Date} UTC. Sample: {Sample}",
                errors.Count,
                date.ToString("yyyy-MM-dd"),
                string.Join("; ", errors.Take(5)));
        }

        return Result<int>.Success(changes);
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
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                return Result<TbankPricePoint?>.Failure(
                    $"TBANK request failed for {instrument.Ticker}/{instrument.Isin}: {(int)response.StatusCode} {TrimBody(body)}");
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
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

            return Result<TbankPricePoint?>.Success(best);
        }
        catch (Exception ex)
        {
            return Result<TbankPricePoint?>.Failure(
                $"TBANK request failed for {instrument.Ticker}/{instrument.Isin}: {ex.Message}");
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

        if (!TryParseDecimal(closeElement, "units", out var units))
        {
            units = 0;
        }

        if (!TryParseDecimal(closeElement, "nano", out var nano))
        {
            nano = 0;
        }

        price = units + (nano / 1_000_000_000m);
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

    private sealed record InstrumentCandidate(Guid Id, string Figi, string CurrencyId, string Ticker, string Isin);
    private sealed record TbankPricePoint(Guid InstrumentId, DateOnly Date, decimal Value, string CurrencyId, string Ticker, string Isin);
}
