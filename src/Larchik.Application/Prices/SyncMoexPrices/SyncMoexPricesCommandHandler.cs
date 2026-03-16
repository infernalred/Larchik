using System.Collections.Concurrent;
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
    private const int MaxTradingStatusParallelism = 6;
    private static readonly string[] PriceColumns =
    [
        "LEGALCLOSEPRICE",
        "MARKETPRICE2",
        "CLOSE",
        "WAPRICE",
        "LCLOSEPRICE",
        "LAST"
    ];
    private const string HistoryColumns =
        "SECID,TRADEDATE,LEGALCLOSEPRICE,MARKETPRICE2,CLOSE,WAPRICE,LCLOSEPRICE,LAST,CURRENCYID,FACEVALUE,FACEUNIT,ACCINT";

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
                x.IsTrading &&
                x.Ticker != null &&
                x.Ticker != "")
            .Select(x => new InstrumentCandidate(x.Id, x.Ticker.ToUpper(), x.CurrencyId.ToUpperInvariant(), x.Type))
            .ToListAsync(cancellationToken);
        var listingHistories = await InstrumentListingHistoryResolver.LoadAsync(
            context,
            instruments.Select(x => x.Id),
            cancellationToken);

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
                "MOEX price sync got no price records for {Date} UTC after loading {LoadedBoards}/{TotalBoards} boards (lookback: {LookbackDays} days); trading flags will still be refreshed",
                date.ToString("yyyy-MM-dd"),
                loadedBoards,
                boards.Length,
                MaxHistoryLookbackDays);
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

        foreach (var listing in listingHistories.Values.SelectMany(x => x))
        {
            if (string.IsNullOrWhiteSpace(listing.Ticker))
            {
                continue;
            }

            if (instrumentsById.TryGetValue(listing.InstrumentId, out var instrument) &&
                !codeMap.ContainsKey(listing.Ticker))
            {
                codeMap[listing.Ticker] = instrument;
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

        var tradingStatusByInstrument = await LoadTradingStates(
            instruments,
            aliasCodes,
            boards,
            baseUrl,
            cancellationToken);

        var trackedInstruments = await context.Instruments
            .Where(x => instrumentIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, cancellationToken);

        var matches = pricesByTicker
            .Select(kvp => codeMap.TryGetValue(kvp.Key, out var instrument)
                ? new MatchedPricePoint(instrument, kvp.Value)
                : null)
            .Where(x => x is not null)
            .Select(x => x!)
            .ToList();

        if (matches.Count == 0)
        {
            var tradingOnlyChanges = ApplyTradingStateUpdates(trackedInstruments, tradingStatusByInstrument);
            var tradingOnlyDbChanges = tradingOnlyChanges == 0 ? 0 : await context.SaveChangesAsync(cancellationToken);
            logger.LogInformation(
                "MOEX price sync finished for {Date} UTC: received {MoexRecords} records, no local instrument matches. Trading flag updates: {TradingUpdates}, db changes: {Changes}",
                date.ToString("yyyy-MM-dd"),
                pricesByTicker.Count,
                tradingOnlyChanges,
                tradingOnlyDbChanges);
            return Result<int>.Success(tradingOnlyDbChanges);
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
            .Concat(listingHistories.Values.SelectMany(x => x).Select(x => x.CurrencyId.ToUpperInvariant()))
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
        var tradingUpdates = 0;

        foreach (var matched in matches)
        {
            var instrument = matched.Instrument;
            var point = matched.Point;
            var activeListing = InstrumentListingHistoryResolver.Resolve(
                instrument.Id,
                instrument.Ticker,
                null,
                null,
                instrument.CurrencyId,
                listingHistories,
                point.Date.ToDateTime(TimeOnly.MinValue));
            var normalizedValue = NormalizeStoredPrice(instrument, point, data);
            var existingKey = new { InstrumentId = instrument.Id, Date = point.Date };

            if (existingByInstrumentDate.TryGetValue(existingKey, out var price))
            {
                price.Value = normalizedValue;
                price.CurrencyId = instrument.CurrencyId;
                price.SourceCurrencyId = point.CurrencyId ?? activeListing.CurrencyId;
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
                    SourceCurrencyId = point.CurrencyId ?? activeListing.CurrencyId,
                    Provider = provider,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                }, cancellationToken);
                inserted++;
            }
        }

        tradingUpdates = ApplyTradingStateUpdates(trackedInstruments, tradingStatusByInstrument);

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
            "Trading flag updates: {TradingUpdates}. Inserted: {Inserted}, updated: {Updated}, db changes: {Changes}",
            date.ToString("yyyy-MM-dd"),
            loadedBoards,
            boards.Length,
            moexRecordsTotal,
            pricesByTicker.Count,
            matches.Count,
            instruments.Count,
            aliasMatchCount,
            sourceDateDistribution.Length == 0 ? "none" : string.Join(", ", sourceDateDistribution),
            tradingUpdates,
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
                            $"{baseUrl}/history/engines/stock/markets/{market}/boards/{board}/securities.json" +
                            $"?date={probeDate:yyyy-MM-dd}&start={start}&iss.meta=off&iss.only=history&history.columns={HistoryColumns}";

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

    private async Task<Dictionary<Guid, bool>> LoadTradingStates(
        IReadOnlyCollection<InstrumentCandidate> instruments,
        IReadOnlyCollection<InstrumentAliasCandidate> aliases,
        IReadOnlyCollection<string> boards,
        string baseUrl,
        CancellationToken cancellationToken)
    {
        var aliasesByInstrument = aliases
            .GroupBy(x => x.InstrumentId)
            .ToDictionary(
                x => x.Key,
                x => x.Select(y => y.NormalizedAliasCode)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray());

        using var client = httpClientFactory.CreateClient();
        var results = new ConcurrentDictionary<Guid, bool>();
        var errors = new ConcurrentBag<string>();
        var semaphore = new SemaphoreSlim(MaxTradingStatusParallelism);

        await Task.WhenAll(instruments.Select(async instrument =>
        {
            await semaphore.WaitAsync(cancellationToken);
            try
            {
                var codes = aliasesByInstrument.TryGetValue(instrument.Id, out var instrumentAliases)
                    ? new[] { instrument.Ticker }.Concat(instrumentAliases).Distinct(StringComparer.OrdinalIgnoreCase).ToArray()
                    : [instrument.Ticker];

                var tradingState = await LoadTradingState(client, codes, boards, baseUrl, cancellationToken);
                if (tradingState.IsSuccess && tradingState.Value.HasValue)
                {
                    results[instrument.Id] = tradingState.Value.Value;
                }
                else if (!tradingState.IsSuccess)
                {
                    errors.Add(tradingState.Error ?? $"MOEX trading status lookup failed for {instrument.Ticker}");
                }
            }
            finally
            {
                semaphore.Release();
            }
        }));

        if (!errors.IsEmpty)
        {
            logger.LogWarning(
                "MOEX trading status lookup had {ErrorCount} errors. Sample: {Sample}",
                errors.Count,
                string.Join("; ", errors.Take(5)));
        }

        return results.ToDictionary(x => x.Key, x => x.Value);
    }

    private async Task<Result<bool?>> LoadTradingState(
        HttpClient client,
        IReadOnlyCollection<string> codes,
        IReadOnlyCollection<string> boards,
        string baseUrl,
        CancellationToken cancellationToken)
    {
        foreach (var code in codes.Where(x => !string.IsNullOrWhiteSpace(x)))
        {
            try
            {
                var url = $"{baseUrl}/securities/{Uri.EscapeDataString(code)}.json?iss.meta=off&iss.only=boards";
                using var response = await client.GetAsync(url, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    continue;
                }

                var json = await response.Content.ReadAsStringAsync(cancellationToken);
                var parseResult = ParseBoardsTradingState(json, boards);
                if (parseResult.IsSuccess)
                {
                    if (parseResult.Value.HasValue)
                    {
                        return parseResult;
                    }

                    continue;
                }

                return parseResult;
            }
            catch (Exception ex)
            {
                return Result<bool?>.Failure($"MOEX trading status request failed for {code}: {ex.Message}");
            }
        }

        return Result<bool?>.Success(null);
    }

    private static Result<bool?> ParseBoardsTradingState(string json, IReadOnlyCollection<string> boards)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("boards", out var boardsElement))
            {
                return Result<bool?>.Failure("MOEX response has no 'boards' section");
            }

            if (!boardsElement.TryGetProperty("columns", out var columnsElement) ||
                columnsElement.ValueKind != JsonValueKind.Array)
            {
                return Result<bool?>.Failure("MOEX response has invalid 'boards.columns'");
            }

            if (!boardsElement.TryGetProperty("data", out var dataElement) || dataElement.ValueKind != JsonValueKind.Array)
            {
                return Result<bool?>.Failure("MOEX response has invalid 'boards.data'");
            }

            var columns = columnsElement
                .EnumerateArray()
                .Select(x => x.GetString() ?? string.Empty)
                .ToArray();

            var boardIdIndex = Array.FindIndex(columns, x => x.Equals("BOARDID", StringComparison.OrdinalIgnoreCase));
            var isTradedIndex = Array.FindIndex(columns, x => x.Equals("IS_TRADED", StringComparison.OrdinalIgnoreCase));
            if (boardIdIndex < 0 || isTradedIndex < 0)
            {
                return Result<bool?>.Failure("MOEX boards response has no BOARDID/IS_TRADED columns");
            }

            var relevantRows = dataElement.EnumerateArray()
                .Where(x => x.ValueKind == JsonValueKind.Array)
                .Select(x => new
                {
                    Board = TryGetStringValue(x, boardIdIndex),
                    IsTraded = TryGetBooleanValue(x, isTradedIndex)
                })
                .Where(x => !string.IsNullOrWhiteSpace(x.Board) &&
                            boards.Contains(x.Board!, StringComparer.OrdinalIgnoreCase) &&
                            x.IsTraded.HasValue)
                .ToArray();

            if (relevantRows.Length == 0)
            {
                return Result<bool?>.Success(null);
            }

            return Result<bool?>.Success(relevantRows.Any(x => x.IsTraded!.Value));
        }
        catch (Exception ex)
        {
            return Result<bool?>.Failure($"Failed to parse MOEX boards response: {ex.Message}");
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

    private static string? TryGetStringValue(JsonElement row, int index)
    {
        if (index < 0 || row.GetArrayLength() <= index)
        {
            return null;
        }

        var cell = row[index];
        return cell.ValueKind switch
        {
            JsonValueKind.String => cell.GetString(),
            JsonValueKind.Number => cell.ToString(),
            _ => null
        };
    }

    private static bool? TryGetBooleanValue(JsonElement row, int index)
    {
        if (index < 0 || row.GetArrayLength() <= index)
        {
            return null;
        }

        var cell = row[index];
        return cell.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Number when cell.TryGetInt32(out var intValue) => intValue != 0,
            JsonValueKind.String when bool.TryParse(cell.GetString(), out var boolValue) => boolValue,
            JsonValueKind.String when int.TryParse(cell.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedInt) => parsedInt != 0,
            _ => null
        };
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

    private static int ApplyTradingStateUpdates(
        IReadOnlyDictionary<Guid, Instrument> trackedInstruments,
        IReadOnlyDictionary<Guid, bool> tradingStatusByInstrument)
    {
        var updates = 0;
        var now = DateTime.UtcNow;

        foreach (var kvp in tradingStatusByInstrument)
        {
            if (!trackedInstruments.TryGetValue(kvp.Key, out var instrument) || instrument.IsTrading == kvp.Value)
            {
                continue;
            }

            instrument.IsTrading = kvp.Value;
            instrument.UpdatedAt = now;
            updates++;
        }

        return updates;
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
