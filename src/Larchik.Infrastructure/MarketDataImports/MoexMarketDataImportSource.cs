using System.Globalization;
using System.Net;
using System.Text.Json;
using Larchik.Application.Helpers;
using Larchik.Application.MarketDataImports.Processing;
using Larchik.Persistence.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Larchik.Infrastructure.MarketDataImports;

public sealed class MoexMarketDataImportSource(
    IHttpClientFactory httpClientFactory,
    IOptions<MarketDataImportSourceOptions> options,
    ILogger<MoexMarketDataImportSource> logger) : IMarketDataImportSource
{
    private const int PageSize = 100;
    private static readonly string[] PriceColumns =
        ["LEGALCLOSEPRICE", "MARKETPRICE2", "CLOSE", "WAPRICE", "LCLOSEPRICE", "LAST"];

    public PriceSource Source => PriceSource.MOEX;

    public async Task<MarketDataSourceResult<ResolvedMarketDataInstrument>> ResolveAsync(
        string isin,
        CancellationToken cancellationToken)
    {
        var normalizedIsin = isin.Trim().ToUpperInvariant();
        try
        {
            using var client = httpClientFactory.CreateClient();
            var searchUrl = $"{BaseUrl}/securities.json?q={Uri.EscapeDataString(normalizedIsin)}&iss.meta=off&iss.only=securities";
            var searchResult = await GetAsync(client, searchUrl, cancellationToken);
            if (!searchResult.IsSuccess || searchResult.Value is null)
            {
                return ForwardFailure<ResolvedMarketDataInstrument>(searchResult);
            }

            var securityResult = ParseSecurity(searchResult.Value, normalizedIsin);
            if (!securityResult.IsSuccess || securityResult.Value is null)
            {
                return ForwardFailure<MoexSecurity, ResolvedMarketDataInstrument>(securityResult);
            }

            var security = securityResult.Value;
            var boardsUrl = $"{BaseUrl}/securities/{Uri.EscapeDataString(security.SecId)}.json?iss.meta=off&iss.only=boards";
            var boardsResult = await GetAsync(client, boardsUrl, cancellationToken);
            if (!boardsResult.IsSuccess || boardsResult.Value is null)
            {
                return ForwardFailure<ResolvedMarketDataInstrument>(boardsResult);
            }

            var boardResult = ParsePrimaryBoard(boardsResult.Value, security.PrimaryBoard);
            if (!boardResult.IsSuccess || boardResult.Value is null)
            {
                return ForwardFailure<MoexBoard, ResolvedMarketDataInstrument>(boardResult);
            }

            var board = boardResult.Value;
            return MarketDataSourceResult<ResolvedMarketDataInstrument>.Success(new ResolvedMarketDataInstrument(
                security.Name,
                security.SecId,
                normalizedIsin,
                null,
                security.Type,
                board.CurrencyId,
                "MOEX",
                ResolveCountry(normalizedIsin),
                security.IsTrading && board.IsTrading,
                security.SecId,
                board.Board,
                board.Engine,
                board.Market,
                board.ListedFrom));
        }
        catch (HttpRequestException ex)
        {
            return MarketDataSourceResult<ResolvedMarketDataInstrument>.TransientFailure($"MOEX request failed: {ex.Message}");
        }
        catch (Exception ex) when (ex is JsonException or FormatException)
        {
            return MarketDataSourceResult<ResolvedMarketDataInstrument>.PermanentFailure($"MOEX response parse failed: {ex.Message}");
        }
    }

    public async Task<MarketDataSourceResult<IReadOnlyCollection<MarketDataImportPricePoint>>> LoadPricesAsync(
        MarketDataImportPriceLoadRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Board) ||
            string.IsNullOrWhiteSpace(request.Engine) ||
            string.IsNullOrWhiteSpace(request.Market))
        {
            return MarketDataSourceResult<IReadOnlyCollection<MarketDataImportPricePoint>>.PermanentFailure(
                $"MOEX route is incomplete for {request.Isin}.");
        }

        try
        {
            using var client = httpClientFactory.CreateClient();
            var points = new List<MarketDataImportPricePoint>();
            for (var start = 0; ; start += PageSize)
            {
                var url = $"{BaseUrl}/history/engines/{Uri.EscapeDataString(request.Engine)}/markets/{Uri.EscapeDataString(request.Market)}/boards/{Uri.EscapeDataString(request.Board)}/securities/{Uri.EscapeDataString(request.SourceInstrumentCode)}.json" +
                          $"?from={request.FromDate:yyyy-MM-dd}&till={request.ToDate:yyyy-MM-dd}&start={start}&iss.meta=off&iss.only=history" +
                          "&history.columns=TRADEDATE,SECID,LEGALCLOSEPRICE,MARKETPRICE2,CLOSE,WAPRICE,LCLOSEPRICE,LAST,CURRENCYID,FACEVALUE,FACEUNIT,ACCINT";
                var pageResult = await GetAsync(client, url, cancellationToken);
                if (!pageResult.IsSuccess || pageResult.Value is null)
                {
                    return ForwardFailure<IReadOnlyCollection<MarketDataImportPricePoint>>(pageResult);
                }

                var parsed = ParseHistory(pageResult.Value, request);
                if (!parsed.IsSuccess || parsed.Value is null)
                {
                    return ForwardFailure<MoexHistoryPage, IReadOnlyCollection<MarketDataImportPricePoint>>(parsed);
                }

                points.AddRange(parsed.Value.Points);
                if (parsed.Value.RawRowCount < PageSize)
                {
                    break;
                }
            }

            logger.LogDebug(
                "MOEX returned {Count} prices for {Isin} from {FromDate} to {ToDate}",
                points.Count,
                request.Isin,
                request.FromDate,
                request.ToDate);
            return MarketDataSourceResult<IReadOnlyCollection<MarketDataImportPricePoint>>.Success(points);
        }
        catch (HttpRequestException ex)
        {
            return MarketDataSourceResult<IReadOnlyCollection<MarketDataImportPricePoint>>.TransientFailure(
                $"MOEX history request failed for {request.Isin}: {ex.Message}");
        }
        catch (Exception ex) when (ex is JsonException or FormatException)
        {
            return MarketDataSourceResult<IReadOnlyCollection<MarketDataImportPricePoint>>.PermanentFailure(
                $"MOEX history response parse failed for {request.Isin}: {ex.Message}");
        }
    }

    private string BaseUrl => options.Value.Moex.BaseUrl.TrimEnd('/');

    private static MarketDataSourceResult<MoexSecurity> ParseSecurity(string json, string isin)
    {
        using var document = JsonDocument.Parse(json);
        if (!TryGetTable(document.RootElement, "securities", out var columns, out var data))
        {
            return MarketDataSourceResult<MoexSecurity>.PermanentFailure("MOEX response has no securities table.");
        }

        var indexes = BuildIndexes(columns);
        foreach (var row in data.EnumerateArray())
        {
            if (!string.Equals(GetString(row, indexes, "isin"), isin, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var secId = GetString(row, indexes, "secid")?.Trim().ToUpperInvariant();
            var name = GetString(row, indexes, "name") ?? GetString(row, indexes, "shortname");
            var type = MapInstrumentType(GetString(row, indexes, "type"), GetString(row, indexes, "group"));
            if (string.IsNullOrWhiteSpace(secId) || string.IsNullOrWhiteSpace(name) || type is null)
            {
                return MarketDataSourceResult<MoexSecurity>.PermanentFailure(
                    $"MOEX metadata for {isin} is incomplete or has unsupported type.");
            }

            return MarketDataSourceResult<MoexSecurity>.Success(new MoexSecurity(
                secId,
                name,
                type.Value,
                GetBoolean(row, indexes, "is_traded") ?? false,
                GetString(row, indexes, "primary_boardid")?.Trim().ToUpperInvariant()));
        }

        return MarketDataSourceResult<MoexSecurity>.PermanentFailure($"MOEX instrument with ISIN {isin} was not found.");
    }

    private static MarketDataSourceResult<MoexBoard> ParsePrimaryBoard(string json, string? preferredBoard)
    {
        using var document = JsonDocument.Parse(json);
        if (!TryGetTable(document.RootElement, "boards", out var columns, out var data))
        {
            return MarketDataSourceResult<MoexBoard>.PermanentFailure("MOEX response has no boards table.");
        }

        var indexes = BuildIndexes(columns);
        var rows = data.EnumerateArray().ToArray();
        var row = rows.FirstOrDefault(x =>
            string.Equals(GetString(x, indexes, "boardid"), preferredBoard, StringComparison.OrdinalIgnoreCase));
        if (row.ValueKind != JsonValueKind.Array)
        {
            row = rows.FirstOrDefault(x => GetBoolean(x, indexes, "is_primary") == true);
        }

        if (row.ValueKind != JsonValueKind.Array)
        {
            row = rows.FirstOrDefault(x => GetBoolean(x, indexes, "is_traded") == true);
        }
        if (row.ValueKind != JsonValueKind.Array)
        {
            return MarketDataSourceResult<MoexBoard>.PermanentFailure("MOEX instrument has no usable board.");
        }

        var board = GetString(row, indexes, "boardid")?.Trim().ToUpperInvariant();
        var engine = GetString(row, indexes, "engine")?.Trim().ToLowerInvariant();
        var market = GetString(row, indexes, "market")?.Trim().ToLowerInvariant();
        var currency = NormalizeCurrency(GetString(row, indexes, "currencyid"));
        if (string.IsNullOrWhiteSpace(board) || string.IsNullOrWhiteSpace(engine) ||
            string.IsNullOrWhiteSpace(market) || string.IsNullOrWhiteSpace(currency))
        {
            return MarketDataSourceResult<MoexBoard>.PermanentFailure("MOEX primary board metadata is incomplete.");
        }

        var listedFrom = ParseDate(GetString(row, indexes, "listed_from")) ??
                         ParseDate(GetString(row, indexes, "history_from"));
        return MarketDataSourceResult<MoexBoard>.Success(new MoexBoard(
            board,
            engine,
            market,
            currency,
            GetBoolean(row, indexes, "is_traded") ?? false,
            listedFrom));
    }

    private static MarketDataSourceResult<MoexHistoryPage> ParseHistory(
        string json,
        MarketDataImportPriceLoadRequest request)
    {
        using var document = JsonDocument.Parse(json);
        if (!TryGetTable(document.RootElement, "history", out var columns, out var data))
        {
            return MarketDataSourceResult<MoexHistoryPage>.PermanentFailure("MOEX response has no history table.");
        }

        var indexes = BuildIndexes(columns);
        var points = new List<MarketDataImportPricePoint>();
        var rawCount = 0;
        foreach (var row in data.EnumerateArray())
        {
            rawCount++;
            var date = ParseDate(GetString(row, indexes, "tradedate"));
            var value = PriceColumns
                .Select(column => GetDecimal(row, indexes, column))
                .FirstOrDefault(x => x is > 0);
            if (date is null || value is null || date < request.FromDate || date > request.ToDate)
            {
                continue;
            }

            var sourceCurrency = NormalizeCurrency(GetString(row, indexes, "currencyid")) ?? request.CurrencyId;
            var storedValue = value.Value;
            if (request.Type == InstrumentType.Bond)
            {
                var faceValue = GetDecimal(row, indexes, "facevalue");
                var faceCurrency = NormalizeCurrency(GetString(row, indexes, "faceunit")) ?? request.CurrencyId;
                if (faceValue is null or <= 0)
                {
                    return MarketDataSourceResult<MoexHistoryPage>.PermanentFailure(
                        $"MOEX returned no face value for bond {request.Isin} on {date:yyyy-MM-dd}.");
                }

                if (!string.Equals(faceCurrency, request.CurrencyId, StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(sourceCurrency, request.CurrencyId, StringComparison.OrdinalIgnoreCase))
                {
                    return MarketDataSourceResult<MoexHistoryPage>.PermanentFailure(
                        $"MOEX bond {request.Isin} requires FX conversion from {faceCurrency}/{sourceCurrency} to {request.CurrencyId}.");
                }

                storedValue = value.Value / 100m * faceValue.Value + (GetDecimal(row, indexes, "accint") ?? 0m);
            }

            points.Add(new MarketDataImportPricePoint(date.Value, storedValue, request.CurrencyId, sourceCurrency));
        }

        return MarketDataSourceResult<MoexHistoryPage>.Success(new MoexHistoryPage(points, rawCount));
    }

    private static async Task<MarketDataSourceResult<string>> GetAsync(
        HttpClient client,
        string url,
        CancellationToken cancellationToken)
    {
        using var response = await client.GetAsync(url, cancellationToken);
        var body = await HttpContentReader.ReadAsStringSafeAsync(response.Content, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            return MarketDataSourceResult<string>.Success(body);
        }

        var error = $"MOEX returned {(int)response.StatusCode} for {url}: {Trim(body)}";
        return IsTransient(response.StatusCode)
            ? MarketDataSourceResult<string>.TransientFailure(error)
            : MarketDataSourceResult<string>.PermanentFailure(error);
    }

    private static bool TryGetTable(
        JsonElement root,
        string name,
        out JsonElement columns,
        out JsonElement data)
    {
        columns = default;
        data = default;
        return root.TryGetProperty(name, out var table) &&
               table.TryGetProperty("columns", out columns) && columns.ValueKind == JsonValueKind.Array &&
               table.TryGetProperty("data", out data) && data.ValueKind == JsonValueKind.Array;
    }

    private static Dictionary<string, int> BuildIndexes(JsonElement columns) => columns
        .EnumerateArray()
        .Select((column, index) => (Name: column.GetString() ?? string.Empty, Index: index))
        .ToDictionary(x => x.Name, x => x.Index, StringComparer.OrdinalIgnoreCase);

    private static string? GetString(JsonElement row, IReadOnlyDictionary<string, int> indexes, string column)
    {
        if (!indexes.TryGetValue(column, out var index) || row.ValueKind != JsonValueKind.Array || row.GetArrayLength() <= index)
        {
            return null;
        }

        var value = row[index];
        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.ToString(),
            _ => null
        };
    }

    private static decimal? GetDecimal(JsonElement row, IReadOnlyDictionary<string, int> indexes, string column)
    {
        var raw = GetString(row, indexes, column);
        if (raw is not null && decimal.TryParse(raw.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed))
        {
            return parsed;
        }

        if (!indexes.TryGetValue(column, out var index) || row.GetArrayLength() <= index)
        {
            return null;
        }

        return row[index].ValueKind == JsonValueKind.Number && row[index].TryGetDecimal(out parsed) ? parsed : null;
    }

    private static bool? GetBoolean(JsonElement row, IReadOnlyDictionary<string, int> indexes, string column)
    {
        var raw = GetString(row, indexes, column);
        return raw?.Trim().ToLowerInvariant() switch
        {
            "1" or "true" => true,
            "0" or "false" => false,
            _ => indexes.TryGetValue(column, out var index) && row.GetArrayLength() > index
                ? row[index].ValueKind switch
                {
                    JsonValueKind.True => true,
                    JsonValueKind.False => false,
                    _ => null
                }
                : null
        };
    }

    private static InstrumentType? MapInstrumentType(string? type, string? group)
    {
        var value = $"{type} {group}".ToLowerInvariant();
        if (value.Contains("bond", StringComparison.Ordinal)) return InstrumentType.Bond;
        if (value.Contains("etf", StringComparison.Ordinal) || value.Contains("ppif", StringComparison.Ordinal) || value.Contains("fund", StringComparison.Ordinal)) return InstrumentType.Etf;
        if (value.Contains("share", StringComparison.Ordinal) || value.Contains("stock", StringComparison.Ordinal)) return InstrumentType.Equity;
        return null;
    }

    private static string? NormalizeCurrency(string? currency) => currency?.Trim().ToUpperInvariant() switch
    {
        "SUR" or "RUR" => "RUB",
        { Length: > 0 } value => value,
        _ => null
    };

    private static DateOnly? ParseDate(string? value) =>
        DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            ? date
            : null;

    private static string? ResolveCountry(string isin) =>
        isin.Length >= 2 && char.IsLetter(isin[0]) && char.IsLetter(isin[1]) ? isin[..2] : null;

    private static bool IsTransient(HttpStatusCode statusCode) =>
        statusCode is HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests || (int)statusCode >= 500;

    private static string Trim(string value) => value.Length <= 300 ? value : value[..300];

    private static MarketDataSourceResult<T> ForwardFailure<T>(MarketDataSourceResult<string> result) =>
        result.IsTransient
            ? MarketDataSourceResult<T>.TransientFailure(result.Error ?? "MOEX request failed.")
            : MarketDataSourceResult<T>.PermanentFailure(result.Error ?? "MOEX request failed.");

    private static MarketDataSourceResult<T> ForwardFailure<TValue, T>(MarketDataSourceResult<TValue> result) =>
        result.IsTransient
            ? MarketDataSourceResult<T>.TransientFailure(result.Error ?? "MOEX request failed.")
            : MarketDataSourceResult<T>.PermanentFailure(result.Error ?? "MOEX request failed.");

    private sealed record MoexSecurity(string SecId, string Name, InstrumentType Type, bool IsTrading, string? PrimaryBoard);
    private sealed record MoexBoard(string Board, string Engine, string Market, string CurrencyId, bool IsTrading, DateOnly? ListedFrom);
    private sealed record MoexHistoryPage(IReadOnlyCollection<MarketDataImportPricePoint> Points, int RawRowCount);
}
