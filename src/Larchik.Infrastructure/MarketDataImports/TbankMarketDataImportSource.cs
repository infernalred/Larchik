using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Larchik.Application.Helpers;
using Larchik.Application.MarketDataImports.Processing;
using Larchik.Persistence.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Larchik.Infrastructure.MarketDataImports;

public sealed class TbankMarketDataImportSource(
    IHttpClientFactory httpClientFactory,
    IOptions<MarketDataImportSourceOptions> options,
    ILogger<TbankMarketDataImportSource> logger) : IMarketDataImportSource
{
    public PriceSource Source => PriceSource.TBANK;

    public async Task<MarketDataSourceResult<ResolvedMarketDataInstrument>> ResolveAsync(
        string isin,
        CancellationToken cancellationToken)
    {
        var normalizedIsin = isin.Trim().ToUpperInvariant();
        var clientResult = CreateClient();
        if (!clientResult.IsSuccess || clientResult.Value is null)
        {
            return ForwardFailure<HttpClient, ResolvedMarketDataInstrument>(clientResult);
        }

        using var client = clientResult.Value;
        var payload = JsonSerializer.Serialize(new
        {
            query = normalizedIsin,
            instrumentKind = "INSTRUMENT_TYPE_UNSPECIFIED",
            apiTradeAvailableFlag = false
        });
        var responseResult = await PostAsync(client, options.Value.Tbank.FindInstrumentBaseUrl, payload, cancellationToken);
        if (!responseResult.IsSuccess || responseResult.Value is null)
        {
            return ForwardFailure<string, ResolvedMarketDataInstrument>(responseResult);
        }

        try
        {
            using var document = JsonDocument.Parse(responseResult.Value);
            if (!TryGetProperty(document.RootElement, "instruments", out var instruments) ||
                instruments.ValueKind != JsonValueKind.Array)
            {
                return MarketDataSourceResult<ResolvedMarketDataInstrument>.PermanentFailure(
                    "T-Bank response has no instruments array.");
            }

            foreach (var item in instruments.EnumerateArray())
            {
                if (!string.Equals(GetString(item, "isin"), normalizedIsin, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var figi = GetString(item, "figi")?.Trim().ToUpperInvariant();
                var ticker = GetString(item, "ticker")?.Trim().ToUpperInvariant();
                var name = GetString(item, "name")?.Trim();
                var currency = GetString(item, "currency")?.Trim().ToUpperInvariant();
                var instrumentType = MapInstrumentType(GetString(item, "instrumentType"));
                if (string.IsNullOrWhiteSpace(figi) || string.IsNullOrWhiteSpace(ticker) ||
                    string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(currency) || instrumentType is null)
                {
                    return MarketDataSourceResult<ResolvedMarketDataInstrument>.PermanentFailure(
                        $"T-Bank metadata for {normalizedIsin} is incomplete or has unsupported type.");
                }

                var classCode = GetString(item, "classCode")?.Trim().ToUpperInvariant();
                return MarketDataSourceResult<ResolvedMarketDataInstrument>.Success(new ResolvedMarketDataInstrument(
                    name,
                    ticker,
                    normalizedIsin,
                    figi,
                    instrumentType.Value,
                    currency,
                    ResolveExchange(classCode),
                    ResolveCountry(normalizedIsin),
                    GetBoolean(item, "apiTradeAvailableFlag") ?? false,
                    figi,
                    classCode,
                    null,
                    null,
                    null));
            }

            return MarketDataSourceResult<ResolvedMarketDataInstrument>.PermanentFailure(
                $"T-Bank instrument with ISIN {normalizedIsin} was not found.");
        }
        catch (JsonException ex)
        {
            return MarketDataSourceResult<ResolvedMarketDataInstrument>.PermanentFailure(
                $"T-Bank instrument response parse failed: {ex.Message}");
        }
    }

    public async Task<MarketDataSourceResult<IReadOnlyCollection<MarketDataImportPricePoint>>> LoadPricesAsync(
        MarketDataImportPriceLoadRequest request,
        CancellationToken cancellationToken)
    {
        var clientResult = CreateClient();
        if (!clientResult.IsSuccess || clientResult.Value is null)
        {
            return ForwardFailure<HttpClient, IReadOnlyCollection<MarketDataImportPricePoint>>(clientResult);
        }

        using var client = clientResult.Value;
        var payload = JsonSerializer.Serialize(new
        {
            from = $"{request.FromDate:yyyy-MM-dd}T00:00:00Z",
            to = $"{request.ToDate:yyyy-MM-dd}T23:59:59Z",
            interval = "CANDLE_INTERVAL_DAY",
            instrumentId = request.SourceInstrumentCode
        });
        var responseResult = await PostAsync(client, options.Value.Tbank.CandlesBaseUrl, payload, cancellationToken);
        if (!responseResult.IsSuccess || responseResult.Value is null)
        {
            return ForwardFailure<string, IReadOnlyCollection<MarketDataImportPricePoint>>(responseResult);
        }

        try
        {
            var candlesResult = ParseCandles(responseResult.Value, request.FromDate, request.ToDate);
            if (!candlesResult.IsSuccess || candlesResult.Value is null)
            {
                return ForwardFailure<List<TbankCandle>, IReadOnlyCollection<MarketDataImportPricePoint>>(candlesResult);
            }

            IReadOnlyCollection<BondAccruedInterest> accruedInterests = [];
            if (request.Type == InstrumentType.Bond && candlesResult.Value.Count > 0)
            {
                var accruedResult = await LoadAccruedInterestsAsync(client, request, cancellationToken);
                if (!accruedResult.IsSuccess || accruedResult.Value is null)
                {
                    return ForwardFailure<IReadOnlyCollection<BondAccruedInterest>, IReadOnlyCollection<MarketDataImportPricePoint>>(accruedResult);
                }

                accruedInterests = accruedResult.Value;
            }

            var points = new List<MarketDataImportPricePoint>(candlesResult.Value.Count);
            foreach (var candle in candlesResult.Value)
            {
                var value = candle.Close;
                if (request.Type == InstrumentType.Bond)
                {
                    var accrued = accruedInterests
                        .Where(x => x.Date <= candle.Date)
                        .MaxBy(x => x.Date);
                    if (accrued is null)
                    {
                        return MarketDataSourceResult<IReadOnlyCollection<MarketDataImportPricePoint>>.PermanentFailure(
                            $"T-Bank returned no accrued interest for bond {request.Isin} on {candle.Date:yyyy-MM-dd}.");
                    }

                    value = candle.Close / 100m * accrued.Nominal + accrued.Value;
                }

                points.Add(new MarketDataImportPricePoint(candle.Date, value, request.CurrencyId, request.CurrencyId));
            }

            logger.LogDebug(
                "T-Bank returned {Count} prices for {Isin} from {FromDate} to {ToDate}",
                points.Count,
                request.Isin,
                request.FromDate,
                request.ToDate);
            return MarketDataSourceResult<IReadOnlyCollection<MarketDataImportPricePoint>>.Success(points);
        }
        catch (JsonException ex)
        {
            return MarketDataSourceResult<IReadOnlyCollection<MarketDataImportPricePoint>>.PermanentFailure(
                $"T-Bank candle response parse failed for {request.Isin}: {ex.Message}");
        }
    }

    private async Task<MarketDataSourceResult<IReadOnlyCollection<BondAccruedInterest>>> LoadAccruedInterestsAsync(
        HttpClient client,
        MarketDataImportPriceLoadRequest request,
        CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Serialize(new
        {
            from = $"{request.FromDate:yyyy-MM-dd}T00:00:00Z",
            to = $"{request.ToDate:yyyy-MM-dd}T23:59:59Z",
            instrumentId = request.SourceInstrumentCode
        });
        var responseResult = await PostAsync(client, options.Value.Tbank.AccruedInterestsBaseUrl, payload, cancellationToken);
        if (!responseResult.IsSuccess || responseResult.Value is null)
        {
            return ForwardFailure<string, IReadOnlyCollection<BondAccruedInterest>>(responseResult);
        }

        try
        {
            using var document = JsonDocument.Parse(responseResult.Value);
            var found = TryGetProperty(document.RootElement, "accruedInterests", out var items) ||
                        TryGetProperty(document.RootElement, "accrued_interests", out items);
            if (!found || items.ValueKind != JsonValueKind.Array)
            {
                return MarketDataSourceResult<IReadOnlyCollection<BondAccruedInterest>>.Success([]);
            }

            var values = new List<BondAccruedInterest>();
            foreach (var item in items.EnumerateArray())
            {
                if (!TryParseDate(GetString(item, "date"), out var date) ||
                    !TryGetProperty(item, "nominal", out var nominalElement) ||
                    !TryParseQuotation(nominalElement, out var nominal) || nominal <= 0 ||
                    !TryGetProperty(item, "value", out var valueElement) ||
                    !TryParseQuotation(valueElement, out var value))
                {
                    continue;
                }

                values.Add(new BondAccruedInterest(date, nominal, value));
            }

            return MarketDataSourceResult<IReadOnlyCollection<BondAccruedInterest>>.Success(values);
        }
        catch (JsonException ex)
        {
            return MarketDataSourceResult<IReadOnlyCollection<BondAccruedInterest>>.PermanentFailure(
                $"T-Bank accrued-interest response parse failed: {ex.Message}");
        }
    }

    private static MarketDataSourceResult<List<TbankCandle>> ParseCandles(
        string json,
        DateOnly fromDate,
        DateOnly toDate)
    {
        using var document = JsonDocument.Parse(json);
        if (!TryGetProperty(document.RootElement, "candles", out var candles) || candles.ValueKind != JsonValueKind.Array)
        {
            return MarketDataSourceResult<List<TbankCandle>>.Success([]);
        }

        var values = new List<TbankCandle>();
        foreach (var candle in candles.EnumerateArray())
        {
            if (!TryParseDate(GetString(candle, "time"), out var date) || date < fromDate || date > toDate ||
                !TryGetProperty(candle, "close", out var close) || !TryParseQuotation(close, out var value) || value <= 0)
            {
                continue;
            }

            values.Add(new TbankCandle(date, value));
        }

        return MarketDataSourceResult<List<TbankCandle>>.Success(values);
    }

    private MarketDataSourceResult<HttpClient> CreateClient()
    {
        var token = options.Value.Tbank.Token?.Trim();
        if (string.IsNullOrWhiteSpace(token))
        {
            return MarketDataSourceResult<HttpClient>.PermanentFailure("T-Bank token is not configured.");
        }

        var client = httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return MarketDataSourceResult<HttpClient>.Success(client);
    }

    private static async Task<MarketDataSourceResult<string>> PostAsync(
        HttpClient client,
        string url,
        string payload,
        CancellationToken cancellationToken)
    {
        try
        {
            using var response = await client.PostAsync(
                url,
                new StringContent(payload, Encoding.UTF8, "application/json"),
                cancellationToken);
            var body = await HttpContentReader.ReadAsStringSafeAsync(response.Content, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                return MarketDataSourceResult<string>.Success(body);
            }

            var error = $"T-Bank returned {(int)response.StatusCode}: {Trim(body)}";
            return IsTransient(response.StatusCode)
                ? MarketDataSourceResult<string>.TransientFailure(error)
                : MarketDataSourceResult<string>.PermanentFailure(error);
        }
        catch (HttpRequestException ex)
        {
            return MarketDataSourceResult<string>.TransientFailure($"T-Bank request failed: {ex.Message}");
        }
    }

    private static string? GetString(JsonElement element, string propertyName)
    {
        if (!TryGetProperty(element, propertyName, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.ToString(),
            _ => null
        };
    }

    private static bool? GetBoolean(JsonElement element, string propertyName)
    {
        if (!TryGetProperty(element, propertyName, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Number when value.TryGetInt32(out var number) => number != 0,
            JsonValueKind.String when bool.TryParse(value.GetString(), out var parsed) => parsed,
            _ => null
        };
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

    private static bool TryParseQuotation(JsonElement element, out decimal value)
    {
        var units = TryParseDecimal(element, "units", out var parsedUnits) ? parsedUnits : 0m;
        var nano = TryParseDecimal(element, "nano", out var parsedNano) ? parsedNano : 0m;
        value = units + nano / 1_000_000_000m;
        return true;
    }

    private static bool TryParseDecimal(JsonElement element, string propertyName, out decimal value)
    {
        value = 0m;
        if (!TryGetProperty(element, propertyName, out var property))
        {
            return false;
        }

        return property.ValueKind switch
        {
            JsonValueKind.Number => property.TryGetDecimal(out value),
            JsonValueKind.String => decimal.TryParse(property.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out value),
            _ => false
        };
    }

    private static bool TryParseDate(string? value, out DateOnly date)
    {
        date = default;
        return DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed) &&
               (date = DateOnly.FromDateTime(parsed.UtcDateTime)) != default;
    }

    private static InstrumentType? MapInstrumentType(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        "share" or "equity" => InstrumentType.Equity,
        "bond" => InstrumentType.Bond,
        "etf" => InstrumentType.Etf,
        "currency" => InstrumentType.Currency,
        _ => null
    };

    private static string? ResolveExchange(string? classCode)
    {
        if (string.IsNullOrWhiteSpace(classCode)) return null;
        if (classCode.StartsWith("SPB", StringComparison.OrdinalIgnoreCase)) return "SPBX";
        if (classCode.StartsWith('T') || classCode.Equals("CETS", StringComparison.OrdinalIgnoreCase)) return "MOEX";
        return null;
    }

    private static string? ResolveCountry(string isin) =>
        isin.Length >= 2 && char.IsLetter(isin[0]) && char.IsLetter(isin[1]) ? isin[..2] : null;

    private static bool IsTransient(HttpStatusCode statusCode) =>
        statusCode is HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests || (int)statusCode >= 500;

    private static string Trim(string value) => value.Length <= 300 ? value : value[..300];

    private static MarketDataSourceResult<TOut> ForwardFailure<TIn, TOut>(MarketDataSourceResult<TIn> result) =>
        result.IsTransient
            ? MarketDataSourceResult<TOut>.TransientFailure(result.Error ?? "T-Bank request failed.")
            : MarketDataSourceResult<TOut>.PermanentFailure(result.Error ?? "T-Bank request failed.");

    private sealed record TbankCandle(DateOnly Date, decimal Close);
    private sealed record BondAccruedInterest(DateOnly Date, decimal Nominal, decimal Value);
}
