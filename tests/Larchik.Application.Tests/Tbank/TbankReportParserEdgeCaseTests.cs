using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using Larchik.Application.Operations.ImportBroker;
using Larchik.Persistence.Entities;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Larchik.Application.Tests.Tbank;

public class TbankReportParserEdgeCaseTests
{
    private static readonly TbankReportParser Parser = new(NullLogger<TbankReportParser>.Instance);

    [Fact]
    public async Task Parse_CashRowWithInvalidDate_AddsErrorAndSkipsOperation()
    {
        await using var stream = CreateWorkbook(
            new WorksheetSpec("Sheet1", "worksheets/sheet1.xml",
                CreateWorksheet(
                    Row(1, (1, "дата"), (2, "операция"), (3, "сумма зачисления"), (4, "примечание")),
                    Row(2, (1, "не дата"), (2, "Пополнение"), (3, "100"), (4, "Тест")))));

        var result = await Parser.ParseAsync(stream, "broker-report-2026-01-01-2026-03-31.xlsx", CancellationToken.None);

        Assert.Empty(result.Operations);
        Assert.Contains(
            "Не удалось распарсить дату денежной операции 'Пополнение' в строке 2",
            result.Errors);
    }

    [Fact]
    public async Task Parse_LegacyCashLayout_ParsesDeposit()
    {
        await using var stream = CreateWorkbook(
            new WorksheetSpec("Sheet1", "worksheets/sheet1.xml",
                CreateWorksheet(
                    Row(1, (1, "2. операции с денежными средствами")),
                    Row(2, (1, "USD")),
                    Row(3, (1, "15.03.2026"), (38, "Пополнение"), (53, "100")))));

        var result = await Parser.ParseAsync(stream, "broker-report-2026-01-01-2026-03-31.xlsx", CancellationToken.None);

        Assert.Empty(result.Errors);
        var operation = Assert.Single(result.Operations);
        Assert.False(operation.RequiresInstrument);
        Assert.Equal(OperationType.Deposit, operation.Operation.Type);
        Assert.Equal("USD", operation.Operation.CurrencyId);
        Assert.Equal(100m, operation.Operation.Price);
        Assert.Equal(new DateTime(2026, 3, 15), operation.Operation.TradeDate.Date);
    }

    [Fact]
    public async Task Parse_UsesWorksheetContainingReportData_NotJustFirstSheet()
    {
        await using var stream = CreateWorkbook(
            new WorksheetSpec("Empty", "worksheets/sheet1.xml",
                CreateWorksheet(Row(1, (1, "служебный лист")))),
            new WorksheetSpec("Report", "worksheets/sheet2.xml",
                CreateWorksheet(
                    Row(1,
                        (1, "Номер сделки"),
                        (2, "Вид сделки"),
                        (3, "Дата заключения"),
                        (4, "Время"),
                        (5, "Код актива"),
                        (6, "Цена за единицу"),
                        (7, "Количество"),
                        (8, "Валюта цены")),
                    Row(2,
                        (1, "1"),
                        (2, "Покупка"),
                        (3, "15.03.2026"),
                        (4, "10:30:00"),
                        (5, "TEST"),
                        (6, "10"),
                        (7, "2"),
                        (8, "RUB")))));

        var result = await Parser.ParseAsync(stream, "broker-report-2026-01-01-2026-03-31.xlsx", CancellationToken.None);

        Assert.Empty(result.Errors);
        var operation = Assert.Single(result.Operations);
        Assert.True(operation.RequiresInstrument);
        Assert.Equal("TEST", operation.InstrumentCode);
        Assert.Equal(OperationType.Buy, operation.Operation.Type);
        Assert.Equal(2m, operation.Operation.Quantity);
        Assert.Equal(10m, operation.Operation.Price);
        Assert.Equal("RUB", operation.Operation.CurrencyId);
        Assert.Equal(new DateTime(2026, 3, 15, 10, 30, 0, DateTimeKind.Utc), operation.Operation.TradeDate);
    }

    [Fact]
    public async Task Parse_ResolvesInstrumentAliasToIsin_FromReferenceSection()
    {
        await using var stream = CreateWorkbook(
            new WorksheetSpec("Report", "worksheets/sheet1.xml",
                CreateWorksheet(
                    Row(1, (1, "Код актива"), (2, "ISIN"), (3, "Наименование актива")),
                    Row(2, (1, "AAPL"), (2, "US0378331005"), (3, "Apple Inc")),
                    Row(4,
                        (1, "Номер сделки"),
                        (2, "Вид сделки"),
                        (3, "Дата заключения"),
                        (4, "Время"),
                        (5, "Код актива"),
                        (6, "Цена за единицу"),
                        (7, "Количество"),
                        (8, "Валюта цены")),
                    Row(5,
                        (1, "1"),
                        (2, "Покупка"),
                        (3, "15.03.2026"),
                        (4, "10:30:00"),
                        (5, "AAPL"),
                        (6, "10"),
                        (7, "2"),
                        (8, "USD")))));

        var result = await Parser.ParseAsync(stream, "broker-report-2026-01-01-2026-03-31.xlsx", CancellationToken.None);

        Assert.Empty(result.Errors);
        var operation = Assert.Single(result.Operations);
        Assert.Equal("US0378331005", operation.InstrumentCode);
    }

    [Fact]
    public async Task Parse_ConvertsBondPercentPrice_ToMoneyPriceUsingDealAmount()
    {
        await using var stream = CreateWorkbook(
            new WorksheetSpec("Report", "worksheets/sheet1.xml",
                CreateWorksheet(
                    Row(1,
                        (1, "Номер сделки"),
                        (2, "Вид сделки"),
                        (3, "Дата заключения"),
                        (4, "Время"),
                        (5, "Код актива"),
                        (6, "Цена за единицу"),
                        (7, "Количество"),
                        (8, "Валюта цены"),
                        (9, "Валюта расчетов"),
                        (10, "Сумма сделки"),
                        (11, "НКД")),
                    Row(2,
                        (1, "1"),
                        (2, "Покупка"),
                        (3, "15.03.2026"),
                        (4, "10:30:00"),
                        (5, "BOND1"),
                        (6, "95"),
                        (7, "2"),
                        (8, "%"),
                        (9, "RUB"),
                        (10, "210"),
                        (11, "10")))));

        var result = await Parser.ParseAsync(stream, "broker-report-2026-01-01-2026-03-31.xlsx", CancellationToken.None);

        Assert.Empty(result.Errors);
        var operation = Assert.Single(result.Operations);
        Assert.Equal(OperationType.Buy, operation.Operation.Type);
        Assert.Equal(105m, operation.Operation.Price);
        Assert.Equal("RUB", operation.Operation.CurrencyId);
    }

    [Fact]
    public async Task Parse_CorporateActionCashNote_CreatesInstrumentOperation()
    {
        await using var stream = CreateWorkbook(
            new WorksheetSpec("Report", "worksheets/sheet1.xml",
                CreateWorksheet(
                    Row(1, (1, "дата"), (2, "операция"), (3, "сумма зачисления"), (4, "примечание")),
                    Row(2,
                        (1, "15.03.2026"),
                        (2, "Корпоративное действие"),
                        (3, "150"),
                        (4, "Тип КД: Частичное погашение; ISIN: RU000A0JX0J2; Количество: 3 шт; Выплата на 1 бумагу: 50")))));

        var result = await Parser.ParseAsync(stream, "broker-report-2026-01-01-2026-03-31.xlsx", CancellationToken.None);

        Assert.Empty(result.Errors);
        var operation = Assert.Single(result.Operations);
        Assert.True(operation.RequiresInstrument);
        Assert.Equal("RU000A0JX0J2", operation.InstrumentCode);
        Assert.Equal(OperationType.BondPartialRedemption, operation.Operation.Type);
        Assert.Equal(3m, operation.Operation.Quantity);
        Assert.Equal(50m, operation.Operation.Price);
        Assert.Equal("RUB", operation.Operation.CurrencyId);
    }

    [Fact]
    public async Task Parse_CorporateActionCashNote_BondPartialRedemption_DoesNotLoseCents_WhenPerUnitAmountAbsent()
    {
        // Cash = quantity * perUnitPrice must not lose cents due to rounding of derived per-unit.
        // Example: 10 / 3 should round to 10.00, not 9.99.
        await using var stream = CreateWorkbook(
            new WorksheetSpec("Report", "worksheets/sheet1.xml",
                CreateWorksheet(
                    Row(1, (1, "дата"), (2, "операция"), (3, "сумма зачисления"), (4, "примечание")),
                    Row(2,
                        (1, "15.03.2026"),
                        (2, "Корпоративное действие"),
                        (3, "10"),
                        (4, "Тип КД: Частичное погашение; ISIN: RU000A0JX0J2; Количество: 3 шт")))));

        var result = await Parser.ParseAsync(stream, "broker-report-2026-01-01-2026-03-31.xlsx", CancellationToken.None);

        Assert.Empty(result.Errors);
        var operation = Assert.Single(result.Operations);
        Assert.Equal(OperationType.BondPartialRedemption, operation.Operation.Type);
        Assert.Equal(3m, operation.Operation.Quantity);

        var roundedPerUnit = decimal.Round(operation.Operation.Price, 2, MidpointRounding.AwayFromZero);
        Assert.NotEqual(roundedPerUnit, operation.Operation.Price); // derived price should not be pre-rounded

        var cashTotalRounded = decimal.Round(operation.Operation.Quantity * operation.Operation.Price, 2, MidpointRounding.AwayFromZero);
        Assert.Equal(10m, cashTotalRounded);
    }

    private static MemoryStream CreateWorkbook(params WorksheetSpec[] worksheets)
    {
        var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteEntry(archive, "_rels/.rels",
                """
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
                  <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/>
                </Relationships>
                """);

            WriteEntry(archive, "[Content_Types].xml",
                """
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
                  <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
                  <Default Extension="xml" ContentType="application/xml"/>
                  <Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/>
                </Types>
                """);

            var workbookNs = XNamespace.Get("http://schemas.openxmlformats.org/spreadsheetml/2006/main");
            var officeNs = XNamespace.Get("http://schemas.openxmlformats.org/officeDocument/2006/relationships");
            var workbook = new XDocument(
                new XElement(workbookNs + "workbook",
                    new XElement(workbookNs + "sheets",
                        worksheets.Select((sheet, index) =>
                            new XElement(workbookNs + "sheet",
                                new XAttribute("name", sheet.Name),
                                new XAttribute("sheetId", index + 1),
                                new XAttribute(officeNs + "id", $"rId{index + 1}"))))));
            WriteEntry(archive, "xl/workbook.xml", workbook.ToString(SaveOptions.DisableFormatting));

            var packageNs = XNamespace.Get("http://schemas.openxmlformats.org/package/2006/relationships");
            var rels = new XDocument(
                new XElement(packageNs + "Relationships",
                    worksheets.Select((sheet, index) =>
                        new XElement(packageNs + "Relationship",
                            new XAttribute("Id", $"rId{index + 1}"),
                            new XAttribute("Type", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet"),
                            new XAttribute("Target", sheet.Target)))));
            WriteEntry(archive, "xl/_rels/workbook.xml.rels", rels.ToString(SaveOptions.DisableFormatting));

            foreach (var worksheet in worksheets)
            {
                WriteEntry(archive, $"xl/{worksheet.Target}", worksheet.Xml);
            }
        }

        stream.Position = 0;
        return stream;
    }

    private static string CreateWorksheet(params XElement[] rows)
    {
        var ns = XNamespace.Get("http://schemas.openxmlformats.org/spreadsheetml/2006/main");
        var document = new XDocument(
            new XElement(ns + "worksheet",
                new XElement(ns + "sheetData", rows)));

        return document.ToString(SaveOptions.DisableFormatting);
    }

    private static XElement Row(int rowNumber, params (int Column, string Value)[] cells)
    {
        var ns = XNamespace.Get("http://schemas.openxmlformats.org/spreadsheetml/2006/main");
        return new XElement(
            ns + "row",
            new XAttribute("r", rowNumber),
            cells.Select(cell =>
                new XElement(ns + "c",
                    new XAttribute("r", $"{ToColumnName(cell.Column)}{rowNumber}"),
                    new XAttribute("t", "inlineStr"),
                    new XElement(ns + "is", new XElement(ns + "t", cell.Value)))));
    }

    private static void WriteEntry(ZipArchive archive, string path, string content)
    {
        var entry = archive.CreateEntry(path);
        using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        writer.Write(content);
    }

    private static string ToColumnName(int columnNumber)
    {
        var column = columnNumber;
        var builder = new StringBuilder();
        while (column > 0)
        {
            column--;
            builder.Insert(0, (char)('A' + column % 26));
            column /= 26;
        }

        return builder.ToString();
    }

    private sealed record WorksheetSpec(string Name, string Target, string Xml);
}
