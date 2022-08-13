using System.Globalization;
using System.Text.RegularExpressions;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using Larchik.Application.Contracts;
using Larchik.Application.Helpers;
using Larchik.Persistence.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Larchik.Application.Reports;

public class CurrencyOperationsReport
{
    public class Query : IRequest<OperationResult<ReportResult>>
    {
        public ReportParams Params { get; set; } = null!;
    }

    public class Handler : IRequestHandler<Query, OperationResult<ReportResult>>
    {
        private readonly ILogger<Handler> _logger;
        private readonly DataContext _context;
        private readonly IUserAccessor _userAccessor;

        public Handler(ILogger<Handler> logger, DataContext context, IUserAccessor userAccessor)
        {
            _logger = logger;
            _context = context;
            _userAccessor = userAccessor;
        }
        
        public async Task<OperationResult<ReportResult>> Handle(Query request, CancellationToken cancellationToken)
        {
            var deals = await _context.Deals
                .Include(x => x.Account)
                .Where(x => x.Account.User.UserName == _userAccessor.GetUsername() 
                            && x.CreatedAt >= request.Params.StartDate 
                            && x.CreatedAt <= request.Params.EndDate 
                            && (x.OperationId == ListOperations.Add || x.OperationId == ListOperations.Dividends))
                .GroupBy(x => new { x.Account.Name, x.CurrencyId }, 
                    (key, group) => new CurrencyDealsReport
                {
                    Account = key.Name,
                    Currency = key.CurrencyId,
                    Amount = group.Sum(x => x.Amount)
                })
                .ToListAsync(cancellationToken);

            await using var ms = new MemoryStream();

            using var sd = SpreadsheetDocument.Create(ms, SpreadsheetDocumentType.Workbook);
            var wbp = sd.AddWorkbookPart();
            wbp.Workbook = new Workbook();
            var wsp = wbp.AddNewPart<WorksheetPart>();
            wsp.Worksheet = new Worksheet(new SheetData());
            var lstColumns = wsp.Worksheet.GetFirstChild<Columns>();
            var needToInsertColumns = false;
            if (lstColumns == null)
            {
                lstColumns = new Columns();
                needToInsertColumns = true;
            }
            lstColumns.Append(new Column { Min = 1, Max = 10, Width = 20, CustomWidth = true });
            lstColumns.Append(new Column { Min = 2, Max = 10, Width = 20, CustomWidth = true });
            lstColumns.Append(new Column { Min = 3, Max = 10, Width = 20, CustomWidth = true });
            if (needToInsertColumns)
                wsp.Worksheet.InsertAt(lstColumns, 0);
            
            var sheets = wbp.Workbook.AppendChild(new Sheets());
            var sheet = new Sheet{ Id = wbp.GetIdOfPart(wsp), SheetId = 1, Name = "Отчет по денежным пополнениям" };
            sheets.Append(sheet);

            var sheetData = wsp.Worksheet.GetFirstChild<SheetData>();


            uint rowIndex = 1;
            
            var row = new Row{ RowIndex = rowIndex };
            sheetData?.Append(row);
            
            InsertCell(row, 1, "Счет", CellValues.String);
            InsertCell(row, 2, "Валюта", CellValues.String);
            InsertCell(row, 3, "Сумма", CellValues.String);

            foreach (var deal in deals)
            {
                row = new Row{ RowIndex = ++rowIndex };
                sheetData?.Append(row);
                InsertCell(row, 1, ReplaceHexadecimalSymbols(deal.Account), CellValues.String);
                InsertCell(row, 2, ReplaceHexadecimalSymbols(deal.Currency), CellValues.String);
                InsertCell(row, 3, deal.Amount.ToString(CultureInfo.InvariantCulture), CellValues.Number);
            }
            
            wbp.Workbook.Save();
            sd.Close();
            
            var result = new ReportResult
            {
                FileName = $"{request.Params.StartDate}_{request.Params.EndDate}.xlsx",
                MimeType = "application/ms-excel",
                FileData = ms.ToArray()
            };

            return OperationResult<ReportResult>.Success(result);
        }
        private static void InsertCell(Row row, int cellNum, string val, CellValues type)
        {
            Cell? refCell = null;
            var newCell = new Cell{ CellReference = cellNum + ":" + row.RowIndex };
            row.InsertBefore(newCell, refCell);
            
            newCell.CellValue = new CellValue(val);
            newCell.DataType = new EnumValue<CellValues>(type);
        }

        private static string ReplaceHexadecimalSymbols(string txt)
        {
            const string r = "[\x00-\x08\x0B\x0C\x0E-\x1F\x26]";
            return Regex.Replace(txt, r, "", RegexOptions.Compiled);
        }
    }
}