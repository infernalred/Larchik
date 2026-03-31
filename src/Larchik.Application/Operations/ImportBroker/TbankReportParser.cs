using System.Globalization;
using System.IO.Compression;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Larchik.Persistence.Entities;
using Microsoft.Extensions.Logging;

namespace Larchik.Application.Operations.ImportBroker;

public class TbankReportParser(ILogger<TbankReportParser> logger) : IBrokerReportParser
{
    public string Code => "tbank";
    private static readonly CultureInfo RuCulture = new("ru-RU");
    private static readonly string InvalidFormatMessage = "Неверный формат файла. Загрузите исходный XLSX-файл отчета Т-Банк.";
    private static readonly string InvalidExtensionMessage = "Неверное расширение файла. Загрузите отчет в формате .xlsx.";
    private static readonly Regex ReportPeriodRegex =
        new(@"(?<start>\d{4}-\d{2}-\d{2})-(?<end>\d{4}-\d{2}-\d{2})", RegexOptions.Compiled);
    private static readonly Regex CorporateActionIsinRegex =
        new(@"ISIN:\s*(?<isin>[A-Z0-9]+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex CorporateActionQuantityRegex =
        new(@"Количество:\s*(?<qty>[0-9]+(?:[.,][0-9]+)?)\s*шт", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex CorporateActionPerUnitRegex =
        new(@"Выплата на 1 бумагу:\s*(?<amount>[0-9]+(?:[.,][0-9]+)?)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

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
            ParseRows(fileStream, fileName, parsed, errors);
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            return Task.FromResult(new BrokerReportParseResult([], [InvalidFormatMessage]));
        }

        return Task.FromResult(new BrokerReportParseResult(parsed, errors));
    }

    private void ParseRows(
        Stream fileStream,
        string fileName,
        ICollection<ParsedOperation> parsed,
        ICollection<string> errors)
    {
        var loadResult = LoadRows(fileStream);
        var rows = loadResult.Rows;
        logger.LogInformation(
            "TBank import: loaded {RowCount} rows from {Source} for file {FileName}",
            rows.Count,
            loadResult.Source,
            fileName);

        var instrumentAliases = BuildInstrumentAliases(rows);
        var reportPeriodEnd = ParseReportPeriodEnd(fileName);
        logger.LogInformation(
            "TBank import: resolved {AliasCount} instrument aliases from report reference sections for file {FileName}",
            instrumentAliases.Count,
            fileName);

        var beforeTrades = parsed.Count;
        ParseTrades(rows, instrumentAliases, parsed, errors);
        var tradesCount = parsed.Count - beforeTrades;

        var beforeCash = parsed.Count;
        ParseCash(rows, parsed, reportPeriodEnd);
        var cashCount = parsed.Count - beforeCash;

        logger.LogInformation(
            "TBank import: parsed {TradesCount} trades and {CashCount} cash operations with {ErrorCount} errors for file {FileName}",
            tradesCount,
            cashCount,
            errors.Count,
            fileName);
    }

    private static LoadRowsResult LoadRows(Stream fileStream)
    {
        // T-Bank XLSX exports can have broken worksheet dimensions and sparse rows,
        // so the importer reads worksheet XML directly instead of relying on a higher-level wrapper.
        return new LoadRowsResult(LoadRowsFromOpenXml(fileStream), "openxml");
    }

    private static List<ReportRow> LoadRowsFromOpenXml(Stream fileStream)
    {
        if (fileStream.CanSeek)
        {
            fileStream.Position = 0;
        }

        using var archive = new ZipArchive(fileStream, ZipArchiveMode.Read, leaveOpen: true);
        var worksheetPath = GetFirstWorksheetPath(archive);
        var worksheetEntry = archive.GetEntry(worksheetPath)
                             ?? throw new InvalidDataException($"Worksheet entry '{worksheetPath}' not found.");

        var sharedStrings = LoadSharedStrings(archive);
        var ns = XNamespace.Get("http://schemas.openxmlformats.org/spreadsheetml/2006/main");

        using var worksheetStream = worksheetEntry.Open();
        var worksheet = XDocument.Load(worksheetStream);
        var sheetRows = worksheet.Root?
            .Element(ns + "sheetData")?
            .Elements(ns + "row")
            .ToList()
            ?? [];

        var rows = new List<ReportRow>(sheetRows.Count);
        var expectedRowNumber = 1;

        foreach (var row in sheetRows)
        {
            var rowNumber = (int?)row.Attribute("r") ?? expectedRowNumber;
            while (expectedRowNumber < rowNumber)
            {
                rows.Add(new ReportRow(expectedRowNumber, new Dictionary<int, string>()));
                expectedRowNumber++;
            }

            var cells = new Dictionary<int, string>();
            foreach (var cell in row.Elements(ns + "c"))
            {
                var reference = (string?)cell.Attribute("r");
                if (string.IsNullOrWhiteSpace(reference))
                {
                    continue;
                }

                var value = GetCellValue(cell, sharedStrings, ns);
                if (string.IsNullOrWhiteSpace(value))
                {
                    continue;
                }

                cells[GetColumnNumber(reference)] = value.Trim();
            }

            rows.Add(new ReportRow(rowNumber, cells));
            expectedRowNumber = rowNumber + 1;
        }

        return rows;
    }

    private static string GetFirstWorksheetPath(ZipArchive archive)
    {
        var workbookEntry = archive.GetEntry("xl/workbook.xml")
                            ?? throw new InvalidDataException("Workbook definition not found.");
        var relsEntry = archive.GetEntry("xl/_rels/workbook.xml.rels")
                        ?? throw new InvalidDataException("Workbook relationships not found.");

        var workbookNs = XNamespace.Get("http://schemas.openxmlformats.org/spreadsheetml/2006/main");
        var officeNs = XNamespace.Get("http://schemas.openxmlformats.org/officeDocument/2006/relationships");
        var packageNs = XNamespace.Get("http://schemas.openxmlformats.org/package/2006/relationships");

        using var workbookStream = workbookEntry.Open();
        using var relsStream = relsEntry.Open();

        var workbook = XDocument.Load(workbookStream);
        var rels = XDocument.Load(relsStream);

        var relationshipId = workbook.Root?
            .Element(workbookNs + "sheets")?
            .Elements(workbookNs + "sheet")
            .Select(sheet => (string?)sheet.Attribute(officeNs + "id"))
            .FirstOrDefault(id => !string.IsNullOrWhiteSpace(id));

        var target = rels.Root?
            .Elements(packageNs + "Relationship")
            .Where(rel => string.Equals((string?)rel.Attribute("Id"), relationshipId, StringComparison.Ordinal))
            .Select(rel => (string?)rel.Attribute("Target"))
            .FirstOrDefault(path => !string.IsNullOrWhiteSpace(path));

        if (string.IsNullOrWhiteSpace(target))
        {
            var fallbackPath = archive.Entries
                .Where(entry => entry.FullName.StartsWith("xl/worksheets/", StringComparison.OrdinalIgnoreCase)
                                && entry.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase)
                                && !entry.FullName.Contains("/_rels/", StringComparison.OrdinalIgnoreCase))
                .Select(entry => entry.FullName)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();

            return fallbackPath ?? throw new InvalidDataException("Worksheet not found.");
        }

        return $"xl/{target.TrimStart('/')}";
    }

    private static Dictionary<int, string> LoadSharedStrings(ZipArchive archive)
    {
        var sharedStringsEntry = archive.GetEntry("xl/sharedStrings.xml");
        if (sharedStringsEntry is null)
        {
            return [];
        }

        var ns = XNamespace.Get("http://schemas.openxmlformats.org/spreadsheetml/2006/main");
        using var stream = sharedStringsEntry.Open();
        var document = XDocument.Load(stream);

        return document.Root?
            .Elements(ns + "si")
            .Select((item, index) => new
            {
                index,
                value = string.Concat(item.Descendants(ns + "t").Select(x => x.Value))
            })
            .ToDictionary(x => x.index, x => x.value)
            ?? [];
    }

    private static string? GetCellValue(XElement cell, IReadOnlyDictionary<int, string> sharedStrings, XNamespace ns)
    {
        var type = (string?)cell.Attribute("t");
        return type switch
        {
            "inlineStr" => string.Concat(cell.Descendants(ns + "t").Select(x => x.Value)),
            "s" => int.TryParse(cell.Element(ns + "v")?.Value, out var index) && sharedStrings.TryGetValue(index, out var sharedValue)
                ? sharedValue
                : null,
            _ => cell.Element(ns + "v")?.Value
        };
    }

    private static int GetColumnNumber(string cellReference)
    {
        var column = 0;
        foreach (var ch in cellReference)
        {
            if (!char.IsLetter(ch))
            {
                break;
            }

            column = column * 26 + char.ToUpperInvariant(ch) - 'A' + 1;
        }

        return column;
    }

    private static Dictionary<string, string> BuildInstrumentAliases(IReadOnlyList<ReportRow> rows)
    {
        var aliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var row in rows)
        {
            var headers = BuildHeaderMap(row);
            if (!headers.ContainsKey("код актива") ||
                !headers.ContainsKey("isin") ||
                !headers.ContainsKey("наименование актива"))
            {
                continue;
            }

            var codeCol = headers["код актива"];
            var isinCol = headers["isin"];
            var startIndex = row.RowNumber + 1;

            for (var i = startIndex; i <= rows.Count; i++)
            {
                var currentRow = rows[i - 1];
                var code = NormalizeCode(currentRow.GetString(codeCol));
                var isin = NormalizeCode(currentRow.GetString(isinCol));

                if (string.IsNullOrWhiteSpace(code) && string.IsNullOrWhiteSpace(isin))
                {
                    break;
                }

                if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(isin))
                {
                    continue;
                }

                aliases[code] = isin;
            }
        }

        return aliases;
    }

    private static void ParseTrades(
        IReadOnlyList<ReportRow> rows,
        IReadOnlyDictionary<string, string> instrumentAliases,
        ICollection<ParsedOperation> parsed,
        ICollection<string> errors)
    {
        var headerRows = rows.Where(r => r.Cells.Any(c => Normalize(c.Value) == "номер сделки")).ToList();
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
            var sumWithoutAccruedCol = headers.GetValueOrDefault("сумма (без нкд)");
            var accruedCol = headers.GetValueOrDefault("нкд");
            var totalDealCol = headers.GetValueOrDefault("сумма сделки");
            var feeBrokerCol = headers.GetValueOrDefault("комиссия брокера");
            var feeBrokerCurCol = headers.GetValueOrDefault("валюта комиссии");
            var feeExchangeCol = headers.GetValueOrDefault("комиссия биржи");
            var feeExchangeCurCol = headers.GetValueOrDefault("валюта комиссии биржи");
            var feeClearCol = headers.GetValueOrDefault("комиссия клир. центра");
            var feeClearCurCol = headers.GetValueOrDefault("валюта комиссии клир. центра");
            var stampDutyCol = headers.GetValueOrDefault("гербовый сбор");
            var stampDutyCurCol = headers.GetValueOrDefault("валюта гербового сбора");
            var settlementDateCol = headers.GetValueOrDefault("дата расчетов план/факт");

            var startIndex = headerRow.RowNumber + 1;
            for (var i = startIndex; i <= rows.Count; i++)
            {
                var row = rows[i - 1];
                var dealId = row.GetString(dealCol);
                if (string.IsNullOrWhiteSpace(dealId))
                {
                    continue;
                }

                var normalizedDealId = Normalize(dealId);
                if (normalizedDealId == "номер сделки" ||
                    normalizedDealId == "валюта" ||
                    normalizedDealId == "дата" ||
                    IsTradeSectionMarker(normalizedDealId))
                {
                    break;
                }

                if (IsTradePager(normalizedDealId))
                {
                    continue;
                }

                var typeText = row.GetString(typeCol);
                var tradeType = ParseTradeType(typeText);
                if (tradeType is null) continue;

                var dateText = row.GetString(dateCol);
                var timeText = timeCol > 0 ? row.GetString(timeCol) : null;
                var tradeDate = ParseDateTime(dateText, timeText);
                if (tradeDate is null)
                {
                    errors.Add($"Не удалось распарсить дату сделки {dealId}");
                    continue;
                }

                var instrumentCode = codeCol > 0 ? NormalizeCode(row.GetString(codeCol)) : null;
                if (instrumentCode is not null && instrumentAliases.TryGetValue(instrumentCode, out var resolvedIsin))
                {
                    instrumentCode = resolvedIsin;
                }

                var price = priceCol > 0 ? row.GetDecimal(priceCol) ?? 0 : 0;
                var quantity = qtyCol > 0 ? row.GetDecimal(qtyCol) ?? 0 : 0;

                var rawPriceCurrency = priceCurrencyCol > 0 ? row.GetString(priceCurrencyCol)?.Trim() : null;
                var priceCurrency = NormalizeCurrency(rawPriceCurrency);
                var settlementCurrency = settlementCurrencyCol > 0 ? NormalizeCurrency(row.GetString(settlementCurrencyCol)) : null;
                var currency = priceCurrency ?? settlementCurrency ?? "RUB";
                var sumWithoutAccrued = sumWithoutAccruedCol > 0 ? row.GetDecimal(sumWithoutAccruedCol) : null;
                var accrued = accruedCol > 0 ? row.GetDecimal(accruedCol) : null;
                var totalDeal = totalDealCol > 0 ? row.GetDecimal(totalDealCol) : null;

                // T-Bank reports bond prices as % of nominal in the trade table.
                // For portfolio accounting we need money-per-bond dirty price.
                if (string.Equals(rawPriceCurrency, "%", StringComparison.OrdinalIgnoreCase) && quantity > 0)
                {
                    var dirtyTradeAmount = totalDeal ?? ((sumWithoutAccrued ?? 0) + (accrued ?? 0));
                    if (dirtyTradeAmount > 0)
                    {
                        price = dirtyTradeAmount / quantity;
                        currency = settlementCurrency ?? "RUB";
                    }
                }

                // T-Bank has two trade table layouts in historical reports:
                // 1) newer files expose итоговую клиентскую комиссию in "Комиссия брокера";
                // 2) older files do not have that column and only expose exchange/clearing/stamp components.
                // For portfolio accounting we should prefer the explicit client-withheld total when present,
                // and only fall back to summing the legacy components when that total column is absent.
                var fee = feeBrokerCol > 0
                    ? SumFee(row, currency, feeBrokerCol, feeBrokerCurCol)
                    : SumFee(row, currency, feeExchangeCol, feeExchangeCurCol)
                      + SumFee(row, currency, feeClearCol, feeClearCurCol)
                      + SumFee(row, currency, stampDutyCol, stampDutyCurCol);

                var settlementText = settlementDateCol > 0 ? row.GetString(settlementDateCol) : null;
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

    private static void ParseCash(
        IReadOnlyList<ReportRow> rows,
        ICollection<ParsedOperation> parsed,
        DateTime? reportPeriodEnd)
    {
        var headerRow = rows.FirstOrDefault(r => Normalize(r.GetString(1)) == "дата"
                                                 && r.Cells.Any(c => Normalize(c.Value) == "операция"));
        if (headerRow is null) return;

        var headers = BuildHeaderMap(headerRow);
        var dateCol = headers.GetValueOrDefault("дата");
        var executionDateCol = headers.GetValueOrDefault("дата исполнения");
        var timeCol = headers.GetValueOrDefault("время совершения");
        var opCol = headers.GetValueOrDefault("операция");
        var incomeCol = headers.GetValueOrDefault("сумма зачисления");
        var outcomeCol = headers.GetValueOrDefault("сумма списания");
        var noteCol = headers.GetValueOrDefault("примечание");

        if (dateCol == 0 || opCol == 0) return;

        var startIndex = headerRow.RowNumber + 1;
        var currentCurrency = FindCurrentCashCurrency(rows, headerRow.RowNumber - 1) ?? "RUB";
        for (var i = startIndex; i <= rows.Count; i++)
        {
            var row = rows[i - 1];
            var rowCurrency = TryGetCashSectionCurrency(row);
            if (rowCurrency is not null)
            {
                currentCurrency = rowCurrency;
                continue;
            }

            var opText = row.GetString(opCol);
            var dateText = row.GetString(dateCol);
            if (string.IsNullOrWhiteSpace(dateText) && executionDateCol > 0)
            {
                dateText = row.GetString(executionDateCol);
            }

            if (string.IsNullOrWhiteSpace(dateText) && string.IsNullOrWhiteSpace(opText)) continue;
            if (string.IsNullOrWhiteSpace(opText)) continue;
            if (string.IsNullOrWhiteSpace(dateText)) continue;

            var timeText = timeCol > 0 ? row.GetString(timeCol) : null;
            var tradeDate = ParseDateTime(dateText, timeText) ?? DateTime.UtcNow;
            if (reportPeriodEnd.HasValue && tradeDate.Date > reportPeriodEnd.Value.Date)
            {
                continue;
            }

            var income = incomeCol > 0 ? row.GetDecimal(incomeCol) ?? 0 : 0;
            var outcome = outcomeCol > 0 ? row.GetDecimal(outcomeCol) ?? 0 : 0;
            var signedAmount = income - outcome;
            if (signedAmount == 0)
            {
                continue;
            }

            var note = noteCol > 0 ? row.GetString(noteCol) : opText;
            var corporateAction = TryParseCorporateAction(note, signedAmount, currentCurrency, tradeDate, opText);
            if (corporateAction is not null)
            {
                parsed.Add(corporateAction);
                continue;
            }

            var mapped = MapCashOperation(opText, signedAmount);
            if (mapped is null)
            {
                continue;
            }

            var operation = new Operation
            {
                Id = Guid.NewGuid(),
                Type = mapped.Value.Type,
                Quantity = 0,
                Price = mapped.Value.Amount,
                Fee = 0,
                CurrencyId = currentCurrency,
                TradeDate = tradeDate,
                SettlementDate = tradeDate,
                Note = string.IsNullOrWhiteSpace(note) ? opText : $"{opText}: {note}",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            parsed.Add(new ParsedOperation(operation, null, false));
        }
    }

    private static ParsedOperation? TryParseCorporateAction(
        string? note,
        decimal signedAmount,
        string currency,
        DateTime tradeDate,
        string opText)
    {
        if (string.IsNullOrWhiteSpace(note))
        {
            return null;
        }

        var normalized = Normalize(note);
        var operationType = normalized switch
        {
            var value when value.Contains("тип кд: частичное погашение") => OperationType.BondPartialRedemption,
            var value when value.Contains("тип кд: погашение в уст. срок") => OperationType.BondMaturity,
            var value when value.Contains("тип кд: выплата дохода по облигациям") => OperationType.Dividend,
            _ => (OperationType?)null
        };

        if (operationType is null)
        {
            return null;
        }

        var isin = NormalizeCode(CorporateActionIsinRegex.Match(note).Groups["isin"].Value);
        if (string.IsNullOrWhiteSpace(isin))
        {
            return null;
        }

        var perUnit = ParseLooseDecimal(CorporateActionPerUnitRegex.Match(note).Groups["amount"].Value);
        if (operationType == OperationType.Dividend)
        {
            var dividendOperation = new Operation
            {
                Id = Guid.NewGuid(),
                Type = OperationType.Dividend,
                Quantity = 0,
                Price = decimal.Abs(signedAmount),
                Fee = 0,
                CurrencyId = currency,
                TradeDate = tradeDate,
                SettlementDate = tradeDate,
                Note = string.IsNullOrWhiteSpace(opText) ? note : $"{opText}: {note}",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            return new ParsedOperation(dividendOperation, isin, true);
        }

        var quantity = ParseLooseDecimal(CorporateActionQuantityRegex.Match(note).Groups["qty"].Value);
        if (quantity is null or <= 0)
        {
            return null;
        }

        var price = perUnit is > 0 ? perUnit.Value : decimal.Abs(signedAmount) / quantity.Value;

        var operation = new Operation
        {
            Id = Guid.NewGuid(),
            Type = operationType.Value,
            Quantity = quantity.Value,
            Price = price,
            Fee = 0,
            CurrencyId = currency,
            TradeDate = tradeDate,
            SettlementDate = tradeDate,
            Note = string.IsNullOrWhiteSpace(opText) ? note : $"{opText}: {note}",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        return new ParsedOperation(operation, isin, true);
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

    private static (OperationType Type, decimal Amount)? MapCashOperation(string value, decimal signedAmount)
    {
        var normalized = Normalize(value);
        if (normalized.Contains("пополнение"))
        {
            return (OperationType.Deposit, decimal.Abs(signedAmount));
        }

        if (normalized.Contains("снятие") || normalized.Contains("вывод"))
        {
            return (OperationType.Withdraw, decimal.Abs(signedAmount));
        }

        if (normalized.Contains("комис"))
        {
            return (OperationType.Fee, decimal.Abs(signedAmount));
        }

        if (normalized.Contains("налог"))
        {
            return (OperationType.Fee, decimal.Abs(signedAmount));
        }

        if (normalized.Contains("дивиденд") ||
            normalized.Contains("выплата доход"))
        {
            return (OperationType.Dividend, decimal.Abs(signedAmount));
        }

        return (OperationType.CashAdjustment, signedAmount);
    }

    private static DateTime? ParseDateTime(string? dateText, string? timeText)
    {
        if (string.IsNullOrWhiteSpace(dateText)) return null;
        var combined = string.IsNullOrWhiteSpace(timeText) ? dateText : $"{dateText} {timeText}";
        return DateTime.TryParse(combined, RuCulture, DateTimeStyles.AssumeLocal, out var dt)
            ? NormalizeImportedDate(dt)
            : null;
    }

    private static DateTime? ParseSettlementDate(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var parts = text.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return DateTime.TryParse(parts.FirstOrDefault(), RuCulture, DateTimeStyles.AssumeLocal, out var dt)
            ? NormalizeImportedDate(dt)
            : null;
    }

    private static string? FindCurrentCashCurrency(IReadOnlyList<ReportRow> rows, int rowNumber)
    {
        for (var i = rowNumber; i >= 1; i--)
        {
            var currency = TryGetCashSectionCurrency(rows[i - 1]);
            if (currency is not null)
            {
                return currency;
            }
        }

        return null;
    }

    private static string? TryGetCashSectionCurrency(ReportRow row)
    {
        if (row.Cells.Count != 1)
        {
            return null;
        }

        return row.Cells.Keys.First() == 1
            ? NormalizeCurrency(row.GetString(1))
            : null;
    }

    private static DateTime? ParseReportPeriodEnd(string fileName)
    {
        var match = ReportPeriodRegex.Match(fileName);
        if (!match.Success)
        {
            return null;
        }

        return DateTime.TryParseExact(
            match.Groups["end"].Value,
            "yyyy-MM-dd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var end)
            ? NormalizeImportedDate(end)
            : null;
    }

    private static decimal SumFee(ReportRow row, string currency, int valueCol, int currencyCol)
    {
        if (valueCol <= 0) return 0;
        var feeCurrency = currencyCol > 0 ? NormalizeCurrency(row.GetString(currencyCol)) ?? currency : currency;
        if (!string.Equals(feeCurrency, currency, StringComparison.OrdinalIgnoreCase)) return 0;
        return row.GetDecimal(valueCol) ?? 0;
    }

    private static decimal? ParseLooseDecimal(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Replace(" ", string.Empty).Replace(',', '.');
        return decimal.TryParse(normalized, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
    }

    private static Dictionary<string, int> BuildHeaderMap(ReportRow row)
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var cell in row.Cells)
        {
            var key = Normalize(cell.Value);
            if (string.IsNullOrWhiteSpace(key)) continue;
            map[key] = cell.Key;
        }
        return map;
    }

    private static string Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : Regex.Replace(value.Replace("\n", " ").Replace("\r", " "), "\\s+", " ")
                .Trim()
                .ToLowerInvariant();

    private static bool IsTradePager(string normalizedValue)
    {
        if (string.IsNullOrWhiteSpace(normalizedValue))
        {
            return false;
        }

        var parts = normalizedValue.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 3 &&
               int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out _) &&
               parts[1] == "из" &&
               int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out _);
    }

    private static bool IsTradeSectionMarker(string normalizedValue) =>
        normalizedValue.Length >= 2 &&
        char.IsDigit(normalizedValue[0]) &&
        normalizedValue[1] == '.';

    private static string? NormalizeCurrency(string? currency)
    {
        if (string.IsNullOrWhiteSpace(currency)) return null;
        var trimmed = currency.Trim().ToUpperInvariant();
        return trimmed.Length == 3 ? trimmed : null;
    }

    private static string? NormalizeCode(string? code) =>
        string.IsNullOrWhiteSpace(code) ? null : code.Trim().ToUpperInvariant();

    private static DateTime NormalizeImportedDate(DateTime value) =>
        value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => DateTime.SpecifyKind(value, DateTimeKind.Utc),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };

    private sealed record ReportRow(int RowNumber, IReadOnlyDictionary<int, string> Cells)
    {
        public string GetString(int column) => Cells.TryGetValue(column, out var value) ? value : string.Empty;

        public decimal? GetDecimal(int column)
        {
            if (!Cells.TryGetValue(column, out var value) || string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            if (decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var invariant))
            {
                return invariant;
            }

            return decimal.TryParse(value, NumberStyles.Number, RuCulture, out var local) ? local : null;
        }
    }

    private sealed record LoadRowsResult(IReadOnlyList<ReportRow> Rows, string Source);
}
