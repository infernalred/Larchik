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

    // Cash amounts are persisted to currency precision to avoid tiny floating/Excel artifacts
    // (e.g. 16514.759999999998 instead of 16514.76) affecting reconciliation/equality.
    private static decimal RoundCashAmount(decimal value) =>
        decimal.Round(value, 2, MidpointRounding.AwayFromZero);

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
        var warnings = new List<string>();
        var parsed = new List<ParsedOperation>();

        try
        {
            ParseRows(fileStream, fileName, parsed, errors, warnings);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogError(ex, "TBank import: failed to parse file {FileName}", fileName);
            return Task.FromResult(new BrokerReportParseResult([], [InvalidFormatMessage]));
        }

        return Task.FromResult(new BrokerReportParseResult(parsed, errors, warnings));
    }

    private void ParseRows(
        Stream fileStream,
        string fileName,
        ICollection<ParsedOperation> parsed,
        ICollection<string> errors,
        ICollection<string> warnings)
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
        ParseCash(rows, parsed, errors, warnings, reportPeriodEnd);
        var cashCount = parsed.Count - beforeCash;

        logger.LogInformation(
            "TBank import: parsed {TradesCount} trades and {CashCount} cash operations with {ErrorCount} errors and {WarningCount} warnings for file {FileName}",
            tradesCount,
            cashCount,
            errors.Count,
            warnings.Count,
            fileName);

        if (warnings.Count > 0)
        {
            logger.LogWarning(
                "TBank import: {WarningCount} cash operations were mapped via fallback CashAdjustment for file {FileName}. First warnings: {Warnings}",
                warnings.Count,
                fileName,
                string.Join("; ", warnings.Take(5)));
        }
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
        var sharedStrings = LoadSharedStrings(archive);
        var worksheetPath = GetWorksheetPath(archive, sharedStrings);
        var worksheetEntry = archive.GetEntry(worksheetPath)
                             ?? throw new InvalidDataException($"Worksheet entry '{worksheetPath}' not found.");

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

    private static string GetWorksheetPath(ZipArchive archive, IReadOnlyDictionary<int, string> sharedStrings)
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

        var targetsByRelationshipId = rels.Root?
            .Elements(packageNs + "Relationship")
            .Where(rel => !string.IsNullOrWhiteSpace((string?)rel.Attribute("Id")))
            .Select(rel => new
            {
                Id = (string?)rel.Attribute("Id"),
                Target = (string?)rel.Attribute("Target")
            })
            .Where(x => !string.IsNullOrWhiteSpace(x.Id) && !string.IsNullOrWhiteSpace(x.Target))
            .ToDictionary(x => x.Id!, x => x.Target!, StringComparer.Ordinal)
            ?? [];

        var worksheetPaths = workbook.Root?
            .Element(workbookNs + "sheets")?
            .Elements(workbookNs + "sheet")
            .Select(sheet => (string?)sheet.Attribute(officeNs + "id"))
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => targetsByRelationshipId.GetValueOrDefault(id!))
            .Where(target => !string.IsNullOrWhiteSpace(target))
            .Select(target => $"xl/{target!.TrimStart('/')}")
            .ToArray()
            ?? [];

        foreach (var worksheetPath in worksheetPaths)
        {
            var worksheetEntry = archive.GetEntry(worksheetPath);
            if (worksheetEntry is null)
            {
                continue;
            }

            if (WorksheetLooksLikeReport(worksheetEntry, sharedStrings))
            {
                return worksheetPath;
            }
        }

        if (worksheetPaths.Length == 0)
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

        return worksheetPaths[0];
    }

    private static bool WorksheetLooksLikeReport(ZipArchiveEntry worksheetEntry, IReadOnlyDictionary<int, string> sharedStrings)
    {
        var ns = XNamespace.Get("http://schemas.openxmlformats.org/spreadsheetml/2006/main");
        using var worksheetStream = worksheetEntry.Open();
        var worksheet = XDocument.Load(worksheetStream);

        var values = worksheet.Root?
            .Element(ns + "sheetData")?
            .Elements(ns + "row")
            .Take(200)
            .SelectMany(row => row.Elements(ns + "c"))
            .Select(cell => Normalize(GetCellValue(cell, sharedStrings, ns)))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToArray()
            ?? [];

        if (values.Length == 0)
        {
            return false;
        }

        var hasTradesMarker = values.Contains("номер сделки", StringComparer.Ordinal);
        var hasCashSectionMarker = values.Contains("2. операции с денежными средствами", StringComparer.Ordinal);
        var hasCashHeader = values.Contains("дата", StringComparer.Ordinal) && values.Contains("операция", StringComparer.Ordinal);

        return hasTradesMarker || hasCashSectionMarker || hasCashHeader;
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
            var layout = new TradeLayout(
                DealColumn: dealCol,
                TypeColumn: typeCol,
                DateColumn: dateCol,
                TimeColumn: timeCol,
                CodeColumn: codeCol,
                PriceColumn: priceCol,
                PriceCurrencyColumn: priceCurrencyCol,
                SettlementCurrencyColumn: settlementCurrencyCol,
                QuantityColumn: qtyCol,
                SumWithoutAccruedColumn: sumWithoutAccruedCol,
                AccruedColumn: accruedCol,
                TotalDealColumn: totalDealCol,
                FeeBrokerColumn: feeBrokerCol,
                FeeBrokerCurrencyColumn: feeBrokerCurCol,
                FeeExchangeColumn: feeExchangeCol,
                FeeExchangeCurrencyColumn: feeExchangeCurCol,
                FeeClearColumn: feeClearCol,
                FeeClearCurrencyColumn: feeClearCurCol,
                StampDutyColumn: stampDutyCol,
                StampDutyCurrencyColumn: stampDutyCurCol,
                SettlementDateColumn: settlementDateCol);

            var startIndex = headerRow.RowNumber + 1;
            for (var i = startIndex; i <= rows.Count; i++)
            {
                var row = rows[i - 1];
                var tradeRow = TryParseTradeRow(row, layout, instrumentAliases);
                if (tradeRow.ShouldStop)
                {
                    break;
                }

                if (tradeRow.Error is not null)
                {
                    errors.Add(tradeRow.Error);
                }

                if (tradeRow.Operation is null)
                {
                    continue;
                }

                parsed.Add(tradeRow.Operation);
            }
        }
    }

    private static void ParseCash(
        IReadOnlyList<ReportRow> rows,
        ICollection<ParsedOperation> parsed,
        ICollection<string> errors,
        ICollection<string> warnings,
        DateTime? reportPeriodEnd)
    {
        var headerRow = rows.FirstOrDefault(r => Normalize(r.GetString(1)) == "дата"
                                                 && r.Cells.Any(c => Normalize(c.Value) == "операция"));
        var layout = headerRow is not null
            ? BuildCashLayoutFromHeader(rows, headerRow)
            : BuildCashLayoutFromSection(rows);
        if (layout is null) return;

        var startIndex = layout.StartRow;
        var currentCurrency = layout.InitialCurrency;
        for (var i = startIndex; i <= rows.Count; i++)
        {
            var row = rows[i - 1];
            if (layout.IsSectionBoundary(row))
            {
                break;
            }

            var rowCurrency = TryGetCashSectionCurrency(row);
            if (rowCurrency is not null)
            {
                currentCurrency = rowCurrency;
                continue;
            }

            var cashRow = TryParseCashRow(row, layout, currentCurrency, reportPeriodEnd);
            if (cashRow.Error is not null)
            {
                errors.Add(cashRow.Error);
            }
            if (cashRow.Warning is not null)
            {
                warnings.Add(cashRow.Warning);
            }

            if (cashRow.NextCurrency is not null)
            {
                currentCurrency = cashRow.NextCurrency;
                continue;
            }

            if (cashRow.Operation is null)
            {
                continue;
            }

            parsed.Add(cashRow.Operation);
        }
    }

    private static TradeRowParseResult TryParseTradeRow(
        ReportRow row,
        TradeLayout layout,
        IReadOnlyDictionary<string, string> instrumentAliases)
    {
        var dealId = row.GetString(layout.DealColumn);
        if (string.IsNullOrWhiteSpace(dealId))
        {
            return TradeRowParseResult.Skip;
        }

        var normalizedDealId = Normalize(dealId);
        if (normalizedDealId == "номер сделки" ||
            normalizedDealId == "валюта" ||
            normalizedDealId == "дата" ||
            IsTradeSectionMarker(normalizedDealId))
        {
            return TradeRowParseResult.Stop;
        }

        if (IsTradePager(normalizedDealId))
        {
            return TradeRowParseResult.Skip;
        }

        var tradeType = ParseTradeType(row.GetString(layout.TypeColumn));
        if (tradeType is null)
        {
            return TradeRowParseResult.Skip;
        }

        var tradeDate = ParseDateTime(
            row.GetString(layout.DateColumn),
            layout.TimeColumn > 0 ? row.GetString(layout.TimeColumn) : null);
        if (tradeDate is null)
        {
            return new TradeRowParseResult(null, $"Не удалось распарсить дату сделки {dealId}", false);
        }

        var instrumentCode = layout.CodeColumn > 0 ? NormalizeCode(row.GetString(layout.CodeColumn)) : null;
        if (instrumentCode is not null && instrumentAliases.TryGetValue(instrumentCode, out var resolvedIsin))
        {
            instrumentCode = resolvedIsin;
        }

        var quantity = layout.QuantityColumn > 0 ? row.GetDecimal(layout.QuantityColumn) ?? 0 : 0;
        var rawPriceCurrency = layout.PriceCurrencyColumn > 0 ? row.GetString(layout.PriceCurrencyColumn)?.Trim() : null;
        var settlementCurrency = layout.SettlementCurrencyColumn > 0 ? NormalizeCurrency(row.GetString(layout.SettlementCurrencyColumn)) : null;
        var tradeMoney = ResolveTradeMoney(row, layout, rawPriceCurrency, settlementCurrency, quantity);
        var fee = ResolveTradeFee(row, tradeMoney.Currency, layout);
        var settlementDate = layout.SettlementDateColumn > 0
            ? ParseSettlementDate(row.GetString(layout.SettlementDateColumn))
            : null;

        return new TradeRowParseResult(
            new ParsedOperation(
                CreateOperation(
                    tradeType.Value,
                    quantity,
                    tradeMoney.Price,
                    fee,
                    tradeMoney.Currency,
                    tradeDate.Value,
                    settlementDate),
                instrumentCode,
                true),
            null,
            false);
    }

    private static CashRowParseResult TryParseCashRow(
        ReportRow row,
        CashLayout layout,
        string currentCurrency,
        DateTime? reportPeriodEnd)
    {
        var rowCurrency = TryGetCashSectionCurrency(row);
        if (rowCurrency is not null)
        {
            return new CashRowParseResult(null, null, rowCurrency, null);
        }

        var opText = row.GetString(layout.OperationColumn);
        var dateText = row.GetString(layout.DateColumn);
        if (string.IsNullOrWhiteSpace(dateText) && layout.ExecutionDateColumn > 0)
        {
            dateText = row.GetString(layout.ExecutionDateColumn);
        }

        if (string.IsNullOrWhiteSpace(dateText) && string.IsNullOrWhiteSpace(opText))
        {
            return CashRowParseResult.Skip;
        }

        if (string.IsNullOrWhiteSpace(opText) ||
            string.IsNullOrWhiteSpace(dateText) ||
            IsCashLayoutMarker(opText, dateText))
        {
            return CashRowParseResult.Skip;
        }

        var tradeDate = ParseDateTime(
            dateText,
            layout.TimeColumn > 0 ? row.GetString(layout.TimeColumn) : null);
        if (tradeDate is null)
        {
            return new CashRowParseResult(
                null,
                $"Не удалось распарсить дату денежной операции '{opText}' в строке {row.RowNumber}",
                null,
                null);
        }

        if (reportPeriodEnd.HasValue && tradeDate.Value.Date > reportPeriodEnd.Value.Date)
        {
            return CashRowParseResult.Skip;
        }

        var income = layout.IncomeColumn > 0 ? row.GetDecimal(layout.IncomeColumn) ?? 0 : 0;
        var outcome = layout.OutcomeColumn > 0 ? row.GetDecimal(layout.OutcomeColumn) ?? 0 : 0;
        var signedAmount = income - outcome;
        if (signedAmount == 0)
        {
            return CashRowParseResult.Skip;
        }

        var note = layout.NoteColumn > 0 ? row.GetString(layout.NoteColumn) : opText;
        var corporateAction = TryParseCorporateAction(note, signedAmount, currentCurrency, tradeDate.Value, opText);
        if (corporateAction is not null)
        {
            return new CashRowParseResult(corporateAction, null, null, null);
        }

        var mapped = MapCashOperation(opText, signedAmount, row.RowNumber);
        if (mapped is null)
        {
            return CashRowParseResult.Skip;
        }

        if (mapped.IsFallback)
        {
            return new CashRowParseResult(
                new ParsedOperation(
                    CreateOperation(
                        mapped.Type,
                        0,
                        RoundCashAmount(mapped.Amount),
                        0,
                        currentCurrency,
                        tradeDate.Value,
                        tradeDate.Value,
                        ComposeNote(opText, note)),
                    null,
                    false),
                null,
                null,
                mapped.WarningMessage);
        }

        return new CashRowParseResult(
            new ParsedOperation(
                CreateOperation(
                    mapped.Type,
                    0,
                    RoundCashAmount(mapped.Amount),
                    0,
                    currentCurrency,
                    tradeDate.Value,
                    tradeDate.Value,
                    ComposeNote(opText, note)),
                null,
                false),
            null,
            null,
            null);
    }

    private static CashLayout? BuildCashLayoutFromHeader(IReadOnlyList<ReportRow> rows, ReportRow headerRow)
    {
        var headers = BuildHeaderMap(headerRow);
        var dateCol = headers.GetValueOrDefault("дата");
        var opCol = headers.GetValueOrDefault("операция");
        if (dateCol == 0 || opCol == 0)
        {
            return null;
        }

        return new CashLayout(
            StartRow: headerRow.RowNumber + 1,
            DateColumn: dateCol,
            TimeColumn: headers.GetValueOrDefault("время совершения"),
            ExecutionDateColumn: headers.GetValueOrDefault("дата исполнения"),
            OperationColumn: opCol,
            IncomeColumn: headers.GetValueOrDefault("сумма зачисления"),
            OutcomeColumn: headers.GetValueOrDefault("сумма списания"),
            NoteColumn: headers.GetValueOrDefault("примечание"),
            InitialCurrency: FindCurrentCashCurrency(rows, headerRow.RowNumber - 1) ?? "RUB",
            IsSectionBoundary: _ => false);
    }

    private static CashLayout? BuildCashLayoutFromSection(IReadOnlyList<ReportRow> rows)
    {
        var cashSectionRow = rows.FirstOrDefault(r => Normalize(r.GetString(1)) == "2. операции с денежными средствами");
        if (cashSectionRow is null)
        {
            return null;
        }

        var detailedCashStart = rows
            .Skip(cashSectionRow.RowNumber)
            .FirstOrDefault(IsPositionedCashDataRow);
        if (detailedCashStart is null)
        {
            return null;
        }

        var initialCurrency = rows
            .Skip(cashSectionRow.RowNumber)
            .Take(detailedCashStart.RowNumber - cashSectionRow.RowNumber)
            .Select(row => NormalizeCurrency(row.GetString(1)))
            .FirstOrDefault(currency => !string.IsNullOrWhiteSpace(currency))
            ?? "RUB";

        return new CashLayout(
            StartRow: detailedCashStart.RowNumber,
            DateColumn: 1,
            TimeColumn: 11,
            ExecutionDateColumn: 23,
            OperationColumn: 38,
            IncomeColumn: 53,
            OutcomeColumn: 66,
            NoteColumn: 77,
            InitialCurrency: initialCurrency,
            IsSectionBoundary: row =>
            {
                var firstCell = Normalize(row.GetString(1));
                return firstCell is "наименование актива" or "наименование контракта";
            });
    }

    private static bool IsPositionedCashDataRow(ReportRow row)
    {
        var operation = row.GetString(38);
        if (string.IsNullOrWhiteSpace(operation))
        {
            return false;
        }

        var dateText = row.GetString(1);
        var executionDateText = row.GetString(23);
        if (string.IsNullOrWhiteSpace(dateText) && string.IsNullOrWhiteSpace(executionDateText))
        {
            return false;
        }

        return row.GetDecimal(53).HasValue || row.GetDecimal(66).HasValue;
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
                Price = RoundCashAmount(decimal.Abs(signedAmount)),
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
            // For bond redemptions the broker cash amount is derived from quantity * unrounded per-unit price.
            // Rounding derived per-unit price can cause a cents-level drift, so keep the per-unit value as-is.
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

    private static CashMapping? MapCashOperation(string value, decimal signedAmount, int rowNumber)
    {
        var normalized = Normalize(value);
        if (normalized.Contains("пополнение"))
        {
            return new CashMapping(OperationType.Deposit, decimal.Abs(signedAmount), false, string.Empty);
        }

        if (normalized.Contains("снятие") || normalized.Contains("вывод"))
        {
            return new CashMapping(OperationType.Withdraw, decimal.Abs(signedAmount), false, string.Empty);
        }

        if (normalized.Contains("комис"))
        {
            return signedAmount >= 0
                ? new CashMapping(OperationType.CashAdjustment, signedAmount, false, string.Empty)
                : new CashMapping(OperationType.Fee, decimal.Abs(signedAmount), false, string.Empty);
        }

        if (normalized.Contains("налог"))
        {
            return signedAmount >= 0
                ? new CashMapping(OperationType.CashAdjustment, signedAmount, false, string.Empty)
                : new CashMapping(OperationType.Fee, decimal.Abs(signedAmount), false, string.Empty);
        }

        if (normalized.Contains("дивиденд") ||
            normalized.Contains("выплата доход"))
        {
            return new CashMapping(OperationType.Dividend, decimal.Abs(signedAmount), false, string.Empty);
        }

        return new CashMapping(
            OperationType.CashAdjustment,
            signedAmount,
            true,
            $"Cash operation fallback to CashAdjustment at row {rowNumber}: '{value}'.");
    }

    private static DateTime? ParseDateTime(string? dateText, string? timeText)
    {
        if (string.IsNullOrWhiteSpace(dateText)) return null;
        var combined = string.IsNullOrWhiteSpace(timeText) ? dateText : $"{dateText} {timeText}";
        return DateTime.TryParse(combined, RuCulture, DateTimeStyles.None, out var dt)
            ? NormalizeImportedDate(dt)
            : null;
    }

    private static DateTime? ParseSettlementDate(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var parts = text.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return DateTime.TryParse(parts.FirstOrDefault(), RuCulture, DateTimeStyles.None, out var dt)
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

    private static bool IsCashLayoutMarker(string opText, string dateText)
    {
        var normalizedOperation = Normalize(opText);
        var normalizedDate = Normalize(dateText);
        var operationLooksLikeDate = DateTime.TryParse(opText, RuCulture, DateTimeStyles.None, out _);

        return normalizedOperation is "операция" or "позиция" or "дата"
               || normalizedOperation.StartsWith("операц", StringComparison.Ordinal)
               || normalizedOperation.StartsWith("позиц", StringComparison.Ordinal)
               || normalizedOperation.StartsWith("дат", StringComparison.Ordinal)
               || normalizedDate == "дата"
               || normalizedDate.StartsWith("дат", StringComparison.Ordinal)
               || operationLooksLikeDate
               || IsTradeSectionMarker(normalizedOperation)
               || IsTradeSectionMarker(normalizedDate);
    }

    private static string? NormalizeCurrency(string? currency)
    {
        if (string.IsNullOrWhiteSpace(currency)) return null;
        var trimmed = currency.Trim().ToUpperInvariant();
        return trimmed.Length == 3 ? trimmed : null;
    }

    private static string? NormalizeCode(string? code) =>
        string.IsNullOrWhiteSpace(code) ? null : code.Trim().ToUpperInvariant();

    // Broker report timestamps are wall-clock values from the report itself.
    // We persist them as UTC without timezone conversion to keep trade dates stable across server locales.
    private static DateTime NormalizeImportedDate(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc);

    private static Operation CreateOperation(
        OperationType type,
        decimal quantity,
        decimal price,
        decimal fee,
        string currency,
        DateTime tradeDate,
        DateTime? settlementDate = null,
        string? note = null)
    {
        var now = DateTime.UtcNow;

        return new Operation
        {
            Id = Guid.NewGuid(),
            Type = type,
            Quantity = quantity,
            Price = price,
            Fee = fee,
            CurrencyId = currency,
            TradeDate = tradeDate,
            SettlementDate = settlementDate ?? tradeDate,
            Note = note,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    private static (decimal Price, string Currency) ResolveTradeMoney(
        ReportRow row,
        TradeLayout layout,
        string? rawPriceCurrency,
        string? settlementCurrency,
        decimal quantity)
    {
        var price = layout.PriceColumn > 0 ? row.GetDecimal(layout.PriceColumn) ?? 0 : 0;
        var priceCurrency = NormalizeCurrency(rawPriceCurrency);
        var currency = priceCurrency ?? settlementCurrency ?? "RUB";
        var sumWithoutAccrued = layout.SumWithoutAccruedColumn > 0 ? row.GetDecimal(layout.SumWithoutAccruedColumn) : null;
        var accrued = layout.AccruedColumn > 0 ? row.GetDecimal(layout.AccruedColumn) : null;
        var totalDeal = layout.TotalDealColumn > 0 ? row.GetDecimal(layout.TotalDealColumn) : null;

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

        return (price, currency);
    }

    private static decimal ResolveTradeFee(ReportRow row, string currency, TradeLayout layout)
    {
        // T-Bank has two trade table layouts in historical reports:
        // 1) newer files expose итоговую клиентскую комиссию in "Комиссия брокера";
        // 2) older files do not have that column and only expose exchange/clearing/stamp components.
        // For portfolio accounting we should prefer the explicit client-withheld total when present,
        // and only fall back to summing the legacy components when that total column is absent.
        return layout.FeeBrokerColumn > 0
            ? SumFee(row, currency, layout.FeeBrokerColumn, layout.FeeBrokerCurrencyColumn)
            : SumFee(row, currency, layout.FeeExchangeColumn, layout.FeeExchangeCurrencyColumn)
              + SumFee(row, currency, layout.FeeClearColumn, layout.FeeClearCurrencyColumn)
              + SumFee(row, currency, layout.StampDutyColumn, layout.StampDutyCurrencyColumn);
    }

    private static string ComposeNote(string opText, string? note) =>
        string.IsNullOrWhiteSpace(note) ? opText : $"{opText}: {note}";

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

    private sealed record CashLayout(
        int StartRow,
        int DateColumn,
        int TimeColumn,
        int ExecutionDateColumn,
        int OperationColumn,
        int IncomeColumn,
        int OutcomeColumn,
        int NoteColumn,
        string InitialCurrency,
        Func<ReportRow, bool> IsSectionBoundary);

    private sealed record TradeLayout(
        int DealColumn,
        int TypeColumn,
        int DateColumn,
        int TimeColumn,
        int CodeColumn,
        int PriceColumn,
        int PriceCurrencyColumn,
        int SettlementCurrencyColumn,
        int QuantityColumn,
        int SumWithoutAccruedColumn,
        int AccruedColumn,
        int TotalDealColumn,
        int FeeBrokerColumn,
        int FeeBrokerCurrencyColumn,
        int FeeExchangeColumn,
        int FeeExchangeCurrencyColumn,
        int FeeClearColumn,
        int FeeClearCurrencyColumn,
        int StampDutyColumn,
        int StampDutyCurrencyColumn,
        int SettlementDateColumn);

    private sealed record TradeRowParseResult(ParsedOperation? Operation, string? Error, bool ShouldStop)
    {
        public static TradeRowParseResult Skip { get; } = new(null, null, false);
        public static TradeRowParseResult Stop { get; } = new(null, null, true);
    }

    private sealed record CashRowParseResult(ParsedOperation? Operation, string? Error, string? NextCurrency, string? Warning)
    {
        public static CashRowParseResult Skip { get; } = new(null, null, null, null);
    }

    private sealed record LoadRowsResult(IReadOnlyList<ReportRow> Rows, string Source);
    private sealed record CashMapping(OperationType Type, decimal Amount, bool IsFallback, string WarningMessage);
}
