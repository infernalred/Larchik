using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Larchik.Application.Helpers;
using Larchik.Persistence.Context;
using Larchik.Persistence.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Larchik.Application.Stocks.SyncTbankInstrumentInfo;

public class SyncTbankInstrumentInfoCommandHandler(
    LarchikContext context,
    IHttpClientFactory httpClientFactory,
    ILogger<SyncTbankInstrumentInfoCommandHandler> logger)
    : IRequestHandler<SyncTbankInstrumentInfoCommand, Result<int>>
{
    private const string DefaultBaseUrl =
        "https://invest-public-api.tbank.ru/rest/tinkoff.public.invest.api.contract.v1.InstrumentsService/GetInstrumentBy";
    private static readonly string[] DefaultCountryExclusions = [];

    public async Task<Result<int>> Handle(SyncTbankInstrumentInfoCommand request, CancellationToken cancellationToken)
    {
        var baseUrl = string.IsNullOrWhiteSpace(request.BaseUrl) ? DefaultBaseUrl : request.BaseUrl.Trim();
        var token = request.Token?.Trim();
        var allowInvalidTls = request.AllowInvalidTls ?? false;
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

        var candidatesQuery = context.Instruments
            .AsNoTracking()
            .Where(x =>
                (x.Type == InstrumentType.Equity || x.Type == InstrumentType.Bond || x.Type == InstrumentType.Etf || x.Type == InstrumentType.Currency) &&
                x.PriceSource == Persistence.Entities.PriceSource.TBANK &&
                x.Figi != null &&
                x.Figi != "");

        if (excludedCountries.Count > 0)
        {
            candidatesQuery = candidatesQuery.Where(x => x.Country == null || !excludedCountries.Contains(x.Country.ToUpper()));
        }

        var instrumentStates = await candidatesQuery
            .Select(x => new InstrumentState(x.Id, x.Figi!, x.Ticker, x.Isin, x.IsTrading, x.Exchange, x.CurrencyId))
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
                    DateTime.UtcNow);
                var figi = string.IsNullOrWhiteSpace(activeListing.Figi) ? x.Figi : activeListing.Figi!;
                return new InstrumentCandidate(x.Id, figi, x.Ticker, x.Isin, x.IsTrading);
            })
            .ToList();

        if (candidates.Count == 0)
        {
            logger.LogInformation("TBANK instrument info sync skipped: no eligible instruments found");
            return Result<int>.Success(0);
        }

        var trackedInstruments = await context.Instruments
            .Where(x => candidates.Select(c => c.Id).Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, cancellationToken);

        using var client = CreateClient(allowInvalidTls);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var updates = new ConcurrentDictionary<Guid, InstrumentTradingInfo>();
        var errors = new ConcurrentBag<string>();
        var missing = 0;
        var semaphore = new SemaphoreSlim(maxParallelism);

        await Task.WhenAll(candidates.Select(async instrument =>
        {
            await semaphore.WaitAsync(cancellationToken);
            try
            {
                var infoResult = await LoadInstrumentInfo(client, instrument, baseUrl, cancellationToken);
                if (infoResult.IsSuccess)
                {
                    if (infoResult.Value is null)
                    {
                        Interlocked.Increment(ref missing);
                    }
                    else
                    {
                        updates[instrument.Id] = infoResult.Value;
                    }
                }
                else
                {
                    errors.Add(infoResult.Error ?? $"TBANK instrument info request failed for {instrument.Ticker}");
                }
            }
            finally
            {
                semaphore.Release();
            }
        }));

        var changed = 0;
        var now = DateTime.UtcNow;
        foreach (var candidate in candidates)
        {
            if (!updates.TryGetValue(candidate.Id, out var info) ||
                !trackedInstruments.TryGetValue(candidate.Id, out var instrument) ||
                instrument.IsTrading == info.IsTrading)
            {
                continue;
            }

            instrument.IsTrading = info.IsTrading;
            instrument.UpdatedAt = now;
            changed++;
        }

        var dbChanges = changed == 0 ? 0 : await context.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "TBANK instrument info sync finished. Eligible instruments: {Eligible}. Loaded: {Loaded}. Missing: {Missing}. Errors: {Errors}. Trading flag updates: {Updated}, db changes: {Changes}",
            candidates.Count,
            updates.Count,
            missing,
            errors.Count,
            changed,
            dbChanges);

        if (!errors.IsEmpty)
        {
            logger.LogWarning(
                "TBANK instrument info sync had {ErrorCount} request errors. Sample: {Sample}",
                errors.Count,
                string.Join("; ", errors.Take(5)));
        }

        return Result<int>.Success(dbChanges);
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

    private async Task<Result<InstrumentTradingInfo?>> LoadInstrumentInfo(
        HttpClient client,
        InstrumentCandidate instrument,
        string baseUrl,
        CancellationToken cancellationToken)
    {
        try
        {
            var payload = JsonSerializer.Serialize(new
            {
                idType = "INSTRUMENT_ID_TYPE_FIGI",
                id = instrument.Figi
            });

            using var response = await client.PostAsync(
                baseUrl,
                new StringContent(payload, Encoding.UTF8, "application/json"),
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var body = await HttpContentReader.ReadAsStringSafeAsync(response.Content, cancellationToken);
                return Result<InstrumentTradingInfo?>.Failure(
                    $"TBANK instrument info request failed for {instrument.Ticker}/{instrument.Isin}: {(int)response.StatusCode} {TrimBody(body)}");
            }

            var json = await HttpContentReader.ReadAsStringSafeAsync(response.Content, cancellationToken);
            return ParseInstrumentInfo(json, instrument);
        }
        catch (Exception ex)
        {
            return Result<InstrumentTradingInfo?>.Failure(
                $"TBANK instrument info request failed for {instrument.Ticker}/{instrument.Isin}: {ex.Message}");
        }
    }

    private static Result<InstrumentTradingInfo?> ParseInstrumentInfo(string json, InstrumentCandidate instrument)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var payload = doc.RootElement;
            if (TryGetProperty(payload, "instrument", out var instrumentElement) &&
                instrumentElement.ValueKind == JsonValueKind.Object)
            {
                payload = instrumentElement;
            }

            var isTrading = TryGetBooleanProperty(payload, "apiTradeAvailableFlag") ??
                            TryGetBooleanProperty(payload, "api_trade_available_flag");

            if (!isTrading.HasValue)
            {
                return Result<InstrumentTradingInfo?>.Success(null);
            }

            var tradingStatus = TryGetStringProperty(payload, "tradingStatus") ??
                                TryGetStringProperty(payload, "trading_status");

            return Result<InstrumentTradingInfo?>.Success(new InstrumentTradingInfo(isTrading.Value, tradingStatus));
        }
        catch (Exception ex)
        {
            return Result<InstrumentTradingInfo?>.Failure(
                $"Failed to parse TBANK instrument info response for {instrument.Ticker}/{instrument.Isin}: {ex.Message}");
        }
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

    private static bool? TryGetBooleanProperty(JsonElement element, string propertyName)
    {
        if (!TryGetProperty(element, propertyName, out var property))
        {
            return null;
        }

        return property.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String when bool.TryParse(property.GetString(), out var boolValue) => boolValue,
            JsonValueKind.Number when property.TryGetInt32(out var intValue) => intValue != 0,
            _ => null
        };
    }

    private static string? TryGetStringProperty(JsonElement element, string propertyName)
    {
        return TryGetProperty(element, propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;
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

    private sealed record InstrumentCandidate(Guid Id, string Figi, string Ticker, string Isin, bool IsTrading);
    private sealed record InstrumentState(Guid Id, string Figi, string Ticker, string Isin, bool IsTrading, string? Exchange, string CurrencyId);
    private sealed record InstrumentTradingInfo(bool IsTrading, string? TradingStatus);
}
