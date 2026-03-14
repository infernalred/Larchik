using System.Globalization;
using System.Text.Json;
using Larchik.Application.Helpers;
using Larchik.Application.Portfolios.Valuation;
using Larchik.Persistence.Context;
using Larchik.Persistence.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Larchik.Application.Prices.SyncMoexPrices;

public class SyncMoexPricesCommandHandler(
    LarchikContext context,
    IHttpClientFactory httpClientFactory,
    ILogger<SyncMoexPricesCommandHandler> logger)
    : IRequestHandler<SyncMoexPricesCommand, Result<int>>
{
    private static readonly string[] DefaultBoards = ["TQBR", "TQTF", "TQIF", "TQCB", "TQOB"];
    private const int MaxHistoryLookbackDays = 7;
    private static readonly string[] PriceColumns =
    [
        "LEGALCLOSEPRICE",
        "MARKETPRICE2",
        "CLOSE",
        "WAPRICE",
        "LCLOSEPRICE",
        "LAST"
    ];

    public async Task<Result<int>> Handle(SyncMoexPricesCommand request, CancellationToken cancellationToken)
    {
        var date = request.Date ?? DateOnly.FromDateTime(DateTime.UtcNow.Date);
        var provider = string.IsNullOrWhiteSpace(request.Provider) ? "MOEX" : request.Provider.Trim().ToUpperInvariant();
        var baseUrl = string.IsNullOrWhiteSpace(request.BaseUrl)
            ? "https://iss.moex.com/iss"
            : request.BaseUrl.Trim().TrimEnd('/');

        var boards = (request.Boards ?? DefaultBoards)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim().ToUpperInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (boards.Length == 0)
        {
            return Result<int>.Failure("MOEX boards list is empty");
        }

        logger.LogInformation(
            "MOEX price sync started for {Date} UTC. Provider: {Provider}. Boards: {Boards}",
            date.ToString("yyyy-MM-dd"),
            provider,
            string.Join(",", boards));

        var instruments = await context.Instruments
            .AsNoTracking()
            .Where(x =>
                (x.Type == InstrumentType.Equity || x.Type == InstrumentType.Bond || x.Type == InstrumentType.Etf) &&
                x.Ticker != null &&
                x.Ticker != "")
            .Select(x => new InstrumentCandidate(x.Id, x.Ticker.ToUpper(), x.CurrencyId.ToUpperInvariant(), x.Type))
            .ToListAsync(cancellationToken);

        if (instruments.Count == 0)
        {
            logger.LogInformation("MOEX price sync skipped for {Date} UTC: no eligible instruments found", date.ToString("yyyy-MM-dd"));
            return Result<int>.Success(0);
        }

        var pricesByTicker = new Dictionary<string, MoexPricePoint>(StringComparer.OrdinalIgnoreCase);
        var boardErrors = new List<string>();
        var loadedBoards = 0;
        var moexRecordsTotal = 0;

        foreach (var board in boards)
        {
            var boardResult = await LoadBoardPrices(date, board, baseUrl, cancellationToken);
            if (!boardResult.IsSuccess)
            {
                boardErrors.Add(boardResult.Error ?? $"Board '{board}' sync failed");
                logger.LogWarning(
                    "MOEX board {Board} sync failed for {Date} UTC: {Error}",
                    board,
                    date.ToString("yyyy-MM-dd"),
                    boardResult.Error);
                continue;
            }

            loadedBoards++;
            moexRecordsTotal += boardResult.Value!.Prices.Count;

            logger.LogInformation(
                "MOEX board {Board} returned {Records} unique records for requested date {RequestedDate} UTC (source trade date: {SourceDate})",
                board,
                boardResult.Value.Prices.Count,
                date.ToString("yyyy-MM-dd"),
                boardResult.Value.SourceDate?.ToString("yyyy-MM-dd") ?? "n/a");

            foreach (var kvp in boardResult.Value.Prices)
            {
                if (!pricesByTicker.ContainsKey(kvp.Key))
                {
                    pricesByTicker[kvp.Key] = kvp.Value;
                }
            }
        }

        if (loadedBoards == 0 && boardErrors.Count > 0)
        {
            return Result<int>.Failure(string.Join("; ", boardErrors));
        }

        if (pricesByTicker.Count == 0)
        {
            logger.LogInformation(
                "MOEX price sync finished for {Date} UTC: loaded boards {LoadedBoards}/{TotalBoards}, but no records returned (lookback: {LookbackDays} days)",
                date.ToString("yyyy-MM-dd"),
                loadedBoards,
                boards.Length,
                MaxHistoryLookbackDays);
            return Result<int>.Success(0);
        }

        var instrumentIds = instruments
            .Select(x => x.Id)
            .ToArray();
        var aliasCodes = await context.InstrumentAliases
            .AsNoTracking()
            .Where(x => instrumentIds.Contains(x.InstrumentId))
            .Select(x => new InstrumentAliasCandidate(x.InstrumentId, x.NormalizedAliasCode))
            .ToListAsync(cancellationToken);

        var instrumentsById = instruments.ToDictionary(x => x.Id);
        var codeMap = new Dictionary<string, InstrumentCandidate>(StringComparer.OrdinalIgnoreCase);
        foreach (var instrument in instruments)
        {
            if (!codeMap.ContainsKey(instrument.Ticker))
            {
                codeMap[instrument.Ticker] = instrument;
            }
        }

        foreach (var alias in aliasCodes)
        {
            if (codeMap.ContainsKey(alias.NormalizedAliasCode))
            {
                continue;
            }

            if (instrumentsById.TryGetValue(alias.InstrumentId, out var instrument))
            {
                codeMap[alias.NormalizedAliasCode] = instrument;
            }
        }

        var matches = pricesByTicker
            .Select(kvp => codeMap.TryGetValue(kvp.Key, out var instrument)
                ? new MatchedPricePoint(instrument, kvp.Value)
                : null)
            .Where(x => x is not null)
            .Select(x => x!)
            .ToList();

        if (matches.Count == 0)
        {
            logger.LogInformation(
                "MOEX price sync finished for {Date} UTC: received {MoexRecords} records, no local instrument matches",
                date.ToString("yyyy-MM-dd"),
                pricesByTicker.Count);
            return Result<int>.Success(0);
        }

        instrumentIds = matches.Select(x => x.Instrument.Id).Distinct().ToArray();
        var sourceDates = matches
            .Select(x => x.Point.Date)
            .Distinct()
            .ToArray();
        var sourceDateValues = sourceDates
            .Select(x => x.ToDateTime(TimeOnly.MinValue).Date)
            .ToArray();
        var neededCurrencies = matches
            .SelectMany(x => new[]
            {
                x.Instrument.CurrencyId,
                x.Point.CurrencyId,
                x.Point.FaceCurrencyId
            })
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x!.ToUpperInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var fxRates = neededCurrencies.Length == 0
            ? []
            : await context.FxRates
                .AsNoTracking()
                .Where(x => neededCurrencies.Contains(x.BaseCurrencyId) && neededCurrencies.Contains(x.QuoteCurrencyId))
                .ToListAsync(cancellationToken);
        var data = new HistoricalDataLookup([], fxRates);

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

        foreach (var matched in matches)
        {
            var instrument = matched.Instrument;
            var point = matched.Point;
            var normalizedValue = NormalizeStoredPrice(instrument, point, data);
            var existingKey = new { InstrumentId = instrument.Id, Date = point.Date };

            if (existingByInstrumentDate.TryGetValue(existingKey, out var price))
            {
                price.Value = normalizedValue;
                price.CurrencyId = instrument.CurrencyId;
                price.Provider = provider;
                price.UpdatedAt = DateTime.UtcNow;
                updated++;
            }
            else
            {
                await context.Prices.AddAsync(new Price
                {
                    Id = Guid.NewGuid(),
                    InstrumentId = instrument.Id,
                    Date = point.Date.ToDateTime(TimeOnly.MinValue),
                    Value = normalizedValue,
                    CurrencyId = instrument.CurrencyId,
                    Provider = provider,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                }, cancellationToken);
                inserted++;
            }
        }

        var changes = await context.SaveChangesAsync(cancellationToken);
        var sourceDateDistribution = matches
            .GroupBy(x => x.Point.Date)
            .OrderBy(x => x.Key)
            .Select(x => $"{x.Key:yyyy-MM-dd}:{x.Count()}")
            .ToArray();
        var aliasMatchCount = matches.Count(x => !string.Equals(x.Instrument.Ticker, x.Point.SecId, StringComparison.OrdinalIgnoreCase));

        logger.LogInformation(
            "MOEX price sync finished for {Date} UTC. Boards loaded: {LoadedBoards}/{TotalBoards}. " +
            "Records from MOEX: {MoexRecordsFromBoards} ({MoexUniqueSecIds} unique). Local matches: {Matches}/{InstrumentCount}. " +
            "Alias matches: {AliasMatches}. " +
            "Matched source dates: {SourceDates}. " +
            "Inserted: {Inserted}, updated: {Updated}, db changes: {Changes}",
            date.ToString("yyyy-MM-dd"),
            loadedBoards,
            boards.Length,
            moexRecordsTotal,
            pricesByTicker.Count,
            matches.Count,
            instruments.Count,
            aliasMatchCount,
            sourceDateDistribution.Length == 0 ? "none" : string.Join(", ", sourceDateDistribution),
            inserted,
            updated,
            changes);

        return Result<int>.Success(changes);
    }

    private async Task<Result<MoexBoardPrices>> LoadBoardPrices(
        DateOnly date,
        string board,
        string baseUrl,
        CancellationToken cancellationToken)
    {
        try
        {
            var client = httpClientFactory.CreateClient();
            var markets = new[] { "shares", "bonds" };
            const int pageSize = 100;

            for (var dayOffset = 0; dayOffset < MaxHistoryLookbackDays; dayOffset++)
            {
                var probeDate = date.AddDays(-dayOffset);
                var prices = new Dictionary<string, MoexPricePoint>(StringComparer.OrdinalIgnoreCase);
                var gotAnyData = false;

                foreach (var market in markets)
                {
                    var marketHasData = false;
                    for (var start = 0; ; start += pageSize)
                    {
                        var url =
                            $"{baseUrl}/history/engines/stock/markets/{market}/boards/{board}/securities.json?date={probeDate:yyyy-MM-dd}&start={start}";

                        var response = await client.GetAsync(url, cancellationToken);
                        if (!response.IsSuccessStatusCode)
                        {
                            if (start == 0) break;
                            return Result<MoexBoardPrices>.Failure(
                                $"MOEX request failed for board '{board}' ({market}): {(int)response.StatusCode}");
                        }

                        var json = await response.Content.ReadAsStringAsync(cancellationToken);
                        var pageResult = ParseHistoryRows(json);
                        if (!pageResult.IsSuccess)
                        {
                            return Result<MoexBoardPrices>.Failure(pageResult.Error ?? "MOEX response parse failed");
                        }

                        var rows = pageResult.Value!;
                        if (rows.Count == 0)
                        {
                            break;
                        }

                        marketHasData = true;
                        gotAnyData = true;

                        foreach (var row in rows)
                        {
                            if (!prices.ContainsKey(row.SecId))
                            {
                                prices[row.SecId] = new MoexPricePoint(
                                    row.SecId,
                                    row.Price,
                                    probeDate,
                                    row.CurrencyId,
                                    row.FaceValue,
                                    row.FaceCurrencyId,
                                    row.AccruedInterest);
                            }
                        }

                        if (rows.Count < pageSize)
                        {
                            break;
                        }
                    }

                    if (marketHasData)
                    {
                        break;
                    }
                }

                if (gotAnyData)
                {
                    return Result<MoexBoardPrices>.Success(new MoexBoardPrices(probeDate, prices));
                }
            }

            return Result<MoexBoardPrices>.Success(new MoexBoardPrices(null, new Dictionary<string, MoexPricePoint>(StringComparer.OrdinalIgnoreCase)));
        }
        catch (Exception ex)
        {
            return Result<MoexBoardPrices>.Failure($"MOEX request failed for board '{board}': {ex.Message}");
        }
    }

    private static Result<List<MoexPriceRow>> ParseHistoryRows(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("history", out var history))
            {
                return Result<List<MoexPriceRow>>.Failure("MOEX response has no 'history' section");
            }

            if (!history.TryGetProperty("columns", out var columnsElement) ||
                columnsElement.ValueKind != JsonValueKind.Array)
            {
                return Result<List<MoexPriceRow>>.Failure("MOEX response has invalid 'history.columns'");
            }

            if (!history.TryGetProperty("data", out var dataElement) || dataElement.ValueKind != JsonValueKind.Array)
            {
                return Result<List<MoexPriceRow>>.Failure("MOEX response has invalid 'history.data'");
            }

            var columns = columnsElement
                .EnumerateArray()
                .Select(x => x.GetString() ?? string.Empty)
                .ToArray();

            var secIdIndex = Array.FindIndex(columns, x => x.Equals("SECID", StringComparison.OrdinalIgnoreCase));
            if (secIdIndex < 0)
            {
                return Result<List<MoexPriceRow>>.Failure("MOEX response has no SECID column");
            }

            var priceIndexes = PriceColumns
                .Select(column => Array.FindIndex(columns, x => x.Equals(column, StringComparison.OrdinalIgnoreCase)))
                .Where(index => index >= 0)
                .ToArray();
            var currencyIdIndex = Array.FindIndex(columns, x => x.Equals("CURRENCYID", StringComparison.OrdinalIgnoreCase));
            var faceValueIndex = Array.FindIndex(columns, x => x.Equals("FACEVALUE", StringComparison.OrdinalIgnoreCase));
            var faceUnitIndex = Array.FindIndex(columns, x => x.Equals("FACEUNIT", StringComparison.OrdinalIgnoreCase));
            var accruedIndex = Array.FindIndex(columns, x => x.Equals("ACCINT", StringComparison.OrdinalIgnoreCase));

            if (priceIndexes.Length == 0)
            {
                return Result<List<MoexPriceRow>>.Failure("MOEX response has no supported price columns");
            }

            var rows = new List<MoexPriceRow>();

            foreach (var rowElement in dataElement.EnumerateArray())
            {
                if (rowElement.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                if (!TryGetString(rowElement, secIdIndex, out var secId) || string.IsNullOrWhiteSpace(secId))
                {
                    continue;
                }

                if (!TryResolvePrice(rowElement, priceIndexes, out var price) || price <= 0)
                {
                    continue;
                }

                var currencyId = TryGetString(rowElement, currencyIdIndex, out var rawCurrencyId)
                    ? NormalizeMoexCurrency(rawCurrencyId)
                    : null;
                var faceCurrencyId = TryGetString(rowElement, faceUnitIndex, out var rawFaceCurrencyId)
                    ? NormalizeMoexCurrency(rawFaceCurrencyId)
                    : null;
                var faceValue = TryGetDecimal(rowElement, faceValueIndex, out var parsedFaceValue)
                    ? parsedFaceValue
                    : (decimal?)null;
                var accruedInterest = TryGetDecimal(rowElement, accruedIndex, out var parsedAccrued)
                    ? parsedAccrued
                    : (decimal?)null;

                rows.Add(new MoexPriceRow(
                    secId.ToUpperInvariant(),
                    price,
                    currencyId,
                    faceValue,
                    faceCurrencyId,
                    accruedInterest));
            }

            return Result<List<MoexPriceRow>>.Success(rows);
        }
        catch (Exception ex)
        {
            return Result<List<MoexPriceRow>>.Failure($"Failed to parse MOEX response: {ex.Message}");
        }
    }

    private static bool TryResolvePrice(JsonElement row, IReadOnlyCollection<int> priceIndexes, out decimal value)
    {
        foreach (var priceIndex in priceIndexes)
        {
            if (TryGetDecimal(row, priceIndex, out value))
            {
                return true;
            }
        }

        value = 0;
        return false;
    }

    private static bool TryGetString(JsonElement row, int index, out string? value)
    {
        value = null;
        if (index < 0 || row.GetArrayLength() <= index)
        {
            return false;
        }

        var cell = row[index];
        if (cell.ValueKind == JsonValueKind.String)
        {
            value = cell.GetString();
            return !string.IsNullOrWhiteSpace(value);
        }

        if (cell.ValueKind == JsonValueKind.Number)
        {
            value = cell.ToString();
            return !string.IsNullOrWhiteSpace(value);
        }

        return false;
    }

    private static bool TryGetDecimal(JsonElement row, int index, out decimal value)
    {
        value = 0;
        if (index < 0 || row.GetArrayLength() <= index)
        {
            return false;
        }

        var cell = row[index];
        if (cell.ValueKind == JsonValueKind.Number)
        {
            return cell.TryGetDecimal(out value);
        }

        if (cell.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        var raw = cell.GetString();
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        var normalized = raw.Replace(',', '.');
        return decimal.TryParse(normalized, NumberStyles.Any, CultureInfo.InvariantCulture, out value);
    }

    private static decimal NormalizeStoredPrice(
        InstrumentCandidate instrument,
        MoexPricePoint point,
        HistoricalDataLookup data)
    {
        if (instrument.Type != InstrumentType.Bond || point.FaceValue is null or <= 0)
        {
            return point.Value;
        }

        var asOfDate = point.Date.ToDateTime(TimeOnly.MinValue);
        var faceCurrency = point.FaceCurrencyId ?? instrument.CurrencyId;
        var tradeCurrency = point.CurrencyId ?? instrument.CurrencyId;

        var cleanAmount = point.Value / 100m * point.FaceValue.Value;
        var cleanInInstrumentCurrency = data.Convert(cleanAmount, faceCurrency, instrument.CurrencyId, asOfDate);
        var accruedInInstrumentCurrency = point.AccruedInterest is > 0
            ? data.Convert(point.AccruedInterest.Value, tradeCurrency, instrument.CurrencyId, asOfDate)
            : 0m;

        return cleanInInstrumentCurrency + accruedInInstrumentCurrency;
    }

    private static string? NormalizeMoexCurrency(string? rawCurrency)
    {
        if (string.IsNullOrWhiteSpace(rawCurrency))
        {
            return null;
        }

        return rawCurrency.Trim().ToUpperInvariant() switch
        {
            "SUR" or "RUR" => "RUB",
            "EUR" => "EUR",
            var value => value
        };
    }

    private sealed record InstrumentCandidate(Guid Id, string Ticker, string CurrencyId, InstrumentType Type);
    private sealed record InstrumentAliasCandidate(Guid InstrumentId, string NormalizedAliasCode);
    private sealed record MatchedPricePoint(InstrumentCandidate Instrument, MoexPricePoint Point);
    private sealed record MoexPricePoint(
        string SecId,
        decimal Value,
        DateOnly Date,
        string? CurrencyId = null,
        decimal? FaceValue = null,
        string? FaceCurrencyId = null,
        decimal? AccruedInterest = null);
    private sealed record MoexBoardPrices(DateOnly? SourceDate, Dictionary<string, MoexPricePoint> Prices);
    private sealed record MoexPriceRow(
        string SecId,
        decimal Price,
        string? CurrencyId,
        decimal? FaceValue,
        string? FaceCurrencyId,
        decimal? AccruedInterest);
}
