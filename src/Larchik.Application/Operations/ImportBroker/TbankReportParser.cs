using System.Globalization;
using ClosedXML.Excel;
using Larchik.Persistence.Entities;

namespace Larchik.Application.Operations.ImportBroker;

public class TbankReportParser : IBrokerReportParser
{
    public string Code => "tbank";
    private static readonly CultureInfo RuCulture = new("ru-RU");
    private static readonly string InvalidFormatMessage = "Неверный формат файла. Загрузите исходный XLSX-файл отчета Т-Банк.";
    private static readonly string InvalidExtensionMessage = "Неверное расширение файла. Загрузите отчет в формате .xlsx.";

    public Task<BrokerReportParseResult> ParseAsync(Stream fileStream, string fileName, CancellationToken cancellationToken)
    {
        var validationError = BrokerReportFileValidator.ValidateXlsx(
            fileStream,
            fileName,
            InvalidExtensionMessage,
            InvalidFormatMessage);

        if (validationError is not null)
        {
            return Task.FromResult(new BrokerReportParseResult([], [validationError]));
        }

        var errors = new List<string>();
        var parsed = new List<ParsedOperation>();

        try
        {
            if (fileStream.CanSeek)
            {
                fileStream.Position = 0;
            }

            using var workbook = new XLWorkbook(fileStream);
            var sheet = workbook.Worksheet(1);
            var rows = sheet.RowsUsed().ToList();

            ParseTrades(rows, parsed, errors);
            ParseCash(rows, parsed);
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            return Task.FromResult(new BrokerReportParseResult([], [InvalidFormatMessage]));
        }

        return Task.FromResult(new BrokerReportParseResult(parsed, errors));
    }

    private static void ParseTrades(IReadOnlyList<IXLRow> rows, ICollection<ParsedOperation> parsed, ICollection<string> errors)
    {
        var headerRows = rows.Where(r => r.CellsUsed().Any(c => Normalize(c.GetString()) == "номер сделки")).ToList();
        foreach (var headerRow in headerRows)
        {
            var headers = BuildHeaderMap(headerRow);
            if (!headers.TryGetValue("номер сделки", out var dealCol) ||
                !headers.TryGetValue("вид сделки", out var typeCol) ||
                !headers.TryGetValue("дата заключения", out var dateCol))
            {
                continue;
            }

            var timeCol = headers.GetValueOrDefault("время");
            var codeCol = headers.GetValueOrDefault("код актива");
            var priceCol = headers.GetValueOrDefault("цена за единицу");
            var priceCurrencyCol = headers.GetValueOrDefault("валюта цены");
            var settlementCurrencyCol = headers.GetValueOrDefault("валюта расчетов");
            var qtyCol = headers.GetValueOrDefault("количество");
            var feeBrokerCol = headers.GetValueOrDefault("комиссия брокера");
            var feeBrokerCurCol = headers.GetValueOrDefault("валюта комиссии");
            var feeExchangeCol = headers.GetValueOrDefault("комиссия биржи");
            var feeExchangeCurCol = headers.GetValueOrDefault("валюта комиссии биржи");
            var feeClearCol = headers.GetValueOrDefault("комиссия клир. центра");
            var feeClearCurCol = headers.GetValueOrDefault("валюта комиссии клир. центра");
            var stampDutyCol = headers.GetValueOrDefault("гербовый сбор");
            var stampDutyCurCol = headers.GetValueOrDefault("валюта гербового сбора");
            var settlementDateCol = headers.GetValueOrDefault("дата расчетов план/факт");

            var startIndex = headerRow.RowNumber() + 1;
            for (var i = startIndex; i <= rows.Count; i++)
            {
                var row = rows[i - 1];
                var dealId = row.Cell(dealCol).GetString();
                if (string.IsNullOrWhiteSpace(dealId)) break;
                if (Normalize(dealId) == "номер сделки" || Normalize(dealId) == "валюта") break;

                var typeText = row.Cell(typeCol).GetString();
                var tradeType = ParseTradeType(typeText);
                if (tradeType is null) continue;

                var dateText = row.Cell(dateCol).GetString();
                var timeText = timeCol > 0 ? row.Cell(timeCol).GetString() : null;
                var tradeDate = ParseDateTime(dateText, timeText);
                if (tradeDate is null)
                {
                    errors.Add($"Не удалось распарсить дату сделки {dealId}");
                    continue;
                }

                var instrumentCode = codeCol > 0 ? NormalizeCode(row.Cell(codeCol).GetString()) : null;

                var price = priceCol > 0 ? row.Cell(priceCol).GetValue<decimal?>() ?? 0 : 0;
                var quantity = qtyCol > 0 ? row.Cell(qtyCol).GetValue<decimal?>() ?? 0 : 0;

                var priceCurrency = priceCurrencyCol > 0 ? NormalizeCurrency(row.Cell(priceCurrencyCol).GetString()) : null;
                var settlementCurrency = settlementCurrencyCol > 0 ? NormalizeCurrency(row.Cell(settlementCurrencyCol).GetString()) : null;
                var currency = priceCurrency ?? settlementCurrency ?? "RUB";

                var fee = SumFee(row, currency, feeBrokerCol, feeBrokerCurCol);
                fee += SumFee(row, currency, feeExchangeCol, feeExchangeCurCol);
                fee += SumFee(row, currency, feeClearCol, feeClearCurCol);
                fee += SumFee(row, currency, stampDutyCol, stampDutyCurCol);

                var settlementText = settlementDateCol > 0 ? row.Cell(settlementDateCol).GetString() : null;
                var settlementDate = ParseSettlementDate(settlementText);

                var op = new Operation
                {
                    Id = Guid.NewGuid(),
                    Type = tradeType.Value,
                    Quantity = quantity,
                    Price = price,
                    Fee = fee,
                    CurrencyId = currency,
                    TradeDate = tradeDate.Value,
                    SettlementDate = settlementDate,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                parsed.Add(new ParsedOperation(op, instrumentCode, true));
            }
        }
    }

    private static void ParseCash(IReadOnlyList<IXLRow> rows, ICollection<ParsedOperation> parsed)
    {
        var headerRow = rows.FirstOrDefault(r => Normalize(r.Cell(1).GetString()) == "дата"
                                                 && r.CellsUsed().Any(c => Normalize(c.GetString()) == "операция"));
        if (headerRow is null) return;

        var headers = BuildHeaderMap(headerRow);
        var dateCol = headers.GetValueOrDefault("дата");
        var timeCol = headers.GetValueOrDefault("время совершения");
        var opCol = headers.GetValueOrDefault("операция");
        var incomeCol = headers.GetValueOrDefault("сумма зачисления");
        var outcomeCol = headers.GetValueOrDefault("сумма списания");
        var noteCol = headers.GetValueOrDefault("примечание");

        if (dateCol == 0 || opCol == 0) return;

        var startIndex = headerRow.RowNumber() + 1;
        for (var i = startIndex; i <= rows.Count; i++)
        {
            var row = rows[i - 1];
            var dateText = row.Cell(dateCol).GetString();
            if (string.IsNullOrWhiteSpace(dateText)) break;

            var opText = row.Cell(opCol).GetString();
            if (string.IsNullOrWhiteSpace(opText)) continue;

            var type = ParseCashOperationType(opText);
            if (type is null) continue;

            var timeText = timeCol > 0 ? row.Cell(timeCol).GetString() : null;
            var tradeDate = ParseDateTime(dateText, timeText) ?? DateTime.UtcNow;

            var income = incomeCol > 0 ? row.Cell(incomeCol).GetValue<decimal?>() ?? 0 : 0;
            var outcome = outcomeCol > 0 ? row.Cell(outcomeCol).GetValue<decimal?>() ?? 0 : 0;
            var amount = income != 0 ? income : outcome;

            var note = noteCol > 0 ? row.Cell(noteCol).GetString() : opText;

            var operation = new Operation
            {
                Id = Guid.NewGuid(),
                Type = type.Value,
                Quantity = 0,
                Price = amount,
                Fee = 0,
                CurrencyId = "RUB",
                TradeDate = tradeDate,
                SettlementDate = tradeDate,
                Note = note,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            parsed.Add(new ParsedOperation(operation, null, false));
        }
    }

    private static OperationType? ParseTradeType(string value)
    {
        return Normalize(value) switch
        {
            "покупка" => OperationType.Buy,
            "продажа" => OperationType.Sell,
            _ => null
        };
    }

    private static OperationType? ParseCashOperationType(string value)
    {
        var normalized = Normalize(value);
        if (normalized.Contains("пополнение")) return OperationType.Deposit;
        if (normalized.Contains("выплата доходов") || normalized.Contains("дивиден")) return OperationType.Dividend;
        if (normalized.Contains("налог")) return OperationType.Fee;
        if (normalized.Contains("снятие") || normalized.Contains("вывод")) return OperationType.Withdraw;
        return null;
    }

    private static DateTime? ParseDateTime(string? dateText, string? timeText)
    {
        if (string.IsNullOrWhiteSpace(dateText)) return null;
        var combined = string.IsNullOrWhiteSpace(timeText) ? dateText : $"{dateText} {timeText}";
        return DateTime.TryParse(combined, RuCulture, DateTimeStyles.AssumeLocal, out var dt) ? dt : null;
    }

    private static DateTime? ParseSettlementDate(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var parts = text.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return DateTime.TryParse(parts.FirstOrDefault(), RuCulture, DateTimeStyles.AssumeLocal, out var dt) ? dt : null;
    }

    private static decimal SumFee(IXLRow row, string currency, int valueCol, int currencyCol)
    {
        if (valueCol <= 0) return 0;
        var feeCurrency = currencyCol > 0 ? NormalizeCurrency(row.Cell(currencyCol).GetString()) ?? currency : currency;
        if (!string.Equals(feeCurrency, currency, StringComparison.OrdinalIgnoreCase)) return 0;
        return row.Cell(valueCol).GetValue<decimal?>() ?? 0;
    }

    private static Dictionary<string, int> BuildHeaderMap(IXLRow row)
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var cell in row.CellsUsed())
        {
            var key = Normalize(cell.GetString());
            if (string.IsNullOrWhiteSpace(key)) continue;
            map[key] = cell.Address.ColumnNumber;
        }
        return map;
    }

    private static string Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Replace("\n", " ").Replace("\r", " ").Trim().ToLowerInvariant();

    private static string? NormalizeCurrency(string? currency)
    {
        if (string.IsNullOrWhiteSpace(currency)) return null;
        var trimmed = currency.Trim().ToUpperInvariant();
        return trimmed.Length == 3 ? trimmed : null;
    }

    private static string? NormalizeCode(string? code) =>
        string.IsNullOrWhiteSpace(code) ? null : code.Trim().ToUpperInvariant();
}
