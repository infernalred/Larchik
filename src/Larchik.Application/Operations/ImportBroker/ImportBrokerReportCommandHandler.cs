using Larchik.Application.Contracts;
using Larchik.Application.Helpers;
using Larchik.Application.Models;
using Larchik.Persistence.Context;
using Larchik.Persistence.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Larchik.Application.Operations.ImportBroker;

public class ImportBrokerReportCommandHandler(
    LarchikContext context,
    IUserAccessor userAccessor,
    IPortfolioRecalcService recalc,
    IEnumerable<IBrokerReportParser> parsers)
    : IRequestHandler<ImportBrokerReportCommand, Result<ImportResultDto>>
{
    public async Task<Result<ImportResultDto>> Handle(ImportBrokerReportCommand request, CancellationToken cancellationToken)
    {
        var userId = userAccessor.GetUserId();
        var portfolio = await context.Portfolios
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == request.PortfolioId && x.UserId == userId, cancellationToken);

        if (portfolio is null)
        {
            return Result<ImportResultDto>.Failure("Портфель не найден или недоступен");
        }

        var parser = parsers.FirstOrDefault(p => p.Code.Equals(request.BrokerCode, StringComparison.OrdinalIgnoreCase));
        if (parser is null)
        {
            return Result<ImportResultDto>.Failure($"Импорт для брокера '{request.BrokerCode}' не настроен");
        }

        var parseResult = await parser.ParseAsync(request.FileStream, request.FileName, cancellationToken);
        if (parseResult.Operations.Count == 0)
        {
            return Result<ImportResultDto>.Failure("В файле не найдено операций");
        }

        var instrumentCodes = parseResult.Operations
            .Where(o => o.RequiresInstrument && !string.IsNullOrWhiteSpace(o.InstrumentCode))
            .Select(o => o.InstrumentCode!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var instrumentMap = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        if (instrumentCodes.Length > 0)
        {
            var instruments = await context.Instruments
                .Where(i => instrumentCodes.Contains(i.Ticker) || instrumentCodes.Contains(i.Isin))
                .ToListAsync(cancellationToken);

            foreach (var instrument in instruments)
            {
                instrumentMap[instrument.Ticker] = instrument.Id;
                instrumentMap[instrument.Isin] = instrument.Id;
            }
        }

        var unresolved = parseResult.Operations
            .Where(o => o.RequiresInstrument)
            .Select(o => o.InstrumentCode ?? "UNKNOWN")
            .Where(code => !instrumentMap.ContainsKey(code))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (unresolved.Length > 0)
        {
            var errors = parseResult.Errors.Concat(unresolved.Select(c => $"Не найден инструмент {c}")).ToArray();
            return Result<ImportResultDto>.Failure(string.Join("; ", errors));
        }

        var operations = new List<Operation>(parseResult.Operations.Count);

        foreach (var parsed in parseResult.Operations)
        {
            if (parsed.RequiresInstrument)
            {
                parsed.Operation.InstrumentId = instrumentMap[parsed.InstrumentCode ?? "UNKNOWN"];
            }

            parsed.Operation.PortfolioId = portfolio.Id;
            operations.Add(parsed.Operation);
        }

        await context.Operations.AddRangeAsync(operations, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        var earliest = operations.Min(o => o.TradeDate);
        await recalc.ScheduleRebuild(portfolio.Id, earliest, cancellationToken);

        var result = new ImportResultDto(
            ImportedOperations: operations.Count,
            SkippedOperations: 0,
            Errors: parseResult.Errors);

        return Result<ImportResultDto>.Success(result);
    }
}
