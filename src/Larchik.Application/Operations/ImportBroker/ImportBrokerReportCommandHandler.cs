using Larchik.Application.Contracts;
using Larchik.Application.Helpers;
using Larchik.Application.Models;
using Larchik.Persistence.Context;
using Larchik.Persistence.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Larchik.Application.Operations.ImportBroker;

public class ImportBrokerReportCommandHandler(
    LarchikContext context,
    IUserAccessor userAccessor,
    IPortfolioRecalcService recalc,
    IEnumerable<IBrokerReportParser> parsers,
    ILogger<ImportBrokerReportCommandHandler> logger)
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
        logger.LogInformation(
            "Broker import: parser {ParserCode} returned {OperationCount} operations and {ErrorCount} errors for file {FileName}",
            parser.Code,
            parseResult.Operations.Count,
            parseResult.Errors.Count,
            request.FileName);

        if (parseResult.Operations.Count == 0)
        {
            if (parseResult.Errors.Count > 0)
            {
                return Result<ImportResultDto>.Failure(string.Join("; ", parseResult.Errors));
            }

            return Result<ImportResultDto>.Failure("В файле не найдено операций");
        }

        var instrumentCodes = parseResult.Operations
            .Where(o => o.RequiresInstrument && !string.IsNullOrWhiteSpace(o.InstrumentCode))
            .Select(o => o.InstrumentCode!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var isinMap = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        var tickerMap = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        var ambiguousTickers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (instrumentCodes.Length > 0)
        {
            var instruments = await context.Instruments
                .Where(i => instrumentCodes.Contains(i.Ticker) || instrumentCodes.Contains(i.Isin))
                .ToListAsync(cancellationToken);

            foreach (var instrument in instruments)
            {
                if (!string.IsNullOrWhiteSpace(instrument.Isin) && !isinMap.ContainsKey(instrument.Isin))
                {
                    isinMap[instrument.Isin] = instrument.Id;
                }
            }

            foreach (var tickerGroup in instruments
                         .Where(x => !string.IsNullOrWhiteSpace(x.Ticker))
                         .GroupBy(x => x.Ticker, StringComparer.OrdinalIgnoreCase))
            {
                var instrumentIds = tickerGroup
                    .Select(x => x.Id)
                    .Distinct()
                    .ToArray();

                if (instrumentIds.Length == 1)
                {
                    tickerMap[tickerGroup.Key] = instrumentIds[0];
                    continue;
                }

                ambiguousTickers.Add(tickerGroup.Key);
            }
        }

        var unresolved = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var ambiguous = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var parsed in parseResult.Operations.Where(x => x.RequiresInstrument))
        {
            var instrumentCode = parsed.InstrumentCode ?? "UNKNOWN";
            if (string.IsNullOrWhiteSpace(parsed.InstrumentCode))
            {
                unresolved.Add(instrumentCode);
                continue;
            }

            if (isinMap.TryGetValue(parsed.InstrumentCode, out _))
            {
                continue;
            }

            if (ambiguousTickers.Contains(parsed.InstrumentCode))
            {
                ambiguous.Add(instrumentCode);
                continue;
            }

            if (!tickerMap.ContainsKey(parsed.InstrumentCode))
            {
                unresolved.Add(instrumentCode);
            }
        }

        if (unresolved.Count > 0 || ambiguous.Count > 0)
        {
            var errors = parseResult.Errors
                .Concat(unresolved.Select(c => $"Не найден инструмент {c}"))
                .Concat(ambiguous.Select(c => $"Найдено несколько инструментов с тикером {c}. Используйте уникальный ISIN."))
                .ToArray();
            return Result<ImportResultDto>.Failure(string.Join("; ", errors));
        }

        var operations = new List<Operation>(parseResult.Operations.Count);

        foreach (var parsed in parseResult.Operations)
        {
            if (parsed.RequiresInstrument)
            {
                parsed.Operation.InstrumentId = isinMap.TryGetValue(parsed.InstrumentCode!, out var isinId)
                    ? isinId
                    : tickerMap[parsed.InstrumentCode!];
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
