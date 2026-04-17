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
            .Include(x => x.Broker)
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

        var normalizedInstrumentCodes = instrumentCodes
            .Select(NormalizeCode)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        var isinMap = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        var tickerMap = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        var aliasMap = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        var ambiguousTickers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var instrumentDetailsById = new Dictionary<Guid, Instrument>();

        if (normalizedInstrumentCodes.Length > 0)
        {
            var aliases = await context.InstrumentAliases
                .Where(x => normalizedInstrumentCodes.Contains(x.NormalizedAliasCode))
                .ToListAsync(cancellationToken);

            foreach (var alias in aliases)
            {
                if (!aliasMap.ContainsKey(alias.AliasCode))
                {
                    aliasMap[alias.AliasCode] = alias.InstrumentId;
                }
            }

            var aliasInstrumentIds = aliases
                .Select(x => x.InstrumentId)
                .Distinct()
                .ToArray();

            var instruments = await context.Instruments
                .Where(i =>
                    instrumentCodes.Contains(i.Ticker) ||
                    instrumentCodes.Contains(i.Isin) ||
                    aliasInstrumentIds.Contains(i.Id))
                .ToListAsync(cancellationToken);

            foreach (var instrument in instruments)
            {
                instrumentDetailsById[instrument.Id] = instrument;
            }

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

            if (aliasMap.TryGetValue(parsed.InstrumentCode, out _))
            {
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

        var operationsToInsert = new List<Operation>(parseResult.Operations.Count);
        var operationsToReconcile = new List<Operation>(parseResult.Operations.Count);
        var importedKeys = new HashSet<string>(StringComparer.Ordinal);
        var skippedCount = 0;
        var reconciledCount = 0;
        var instrumentCodeById = new Dictionary<Guid, string>();
        var baseKeyOccurrences = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var instrument in instrumentDetailsById.Values)
        {
            instrumentCodeById[instrument.Id] = !string.IsNullOrWhiteSpace(instrument.Isin)
                ? instrument.Isin
                : instrument.Ticker;
        }

        foreach (var parsed in parseResult.Operations)
        {
            if (parsed.RequiresInstrument)
            {
                parsed.Operation.InstrumentId = aliasMap.TryGetValue(parsed.InstrumentCode!, out var aliasId)
                    ? aliasId
                    : isinMap.TryGetValue(parsed.InstrumentCode!, out var isinId)
                        ? isinId
                        : tickerMap[parsed.InstrumentCode!];
            }

            parsed.Operation.PortfolioId = portfolio.Id;
            var canonicalInstrumentCode = parsed.Operation.InstrumentId is { } instrumentId
                ? instrumentCodeById[instrumentId]
                : null;
            var baseKey = BrokerOperationKeyBuilder.BuildBaseHash(parsed.Operation, canonicalInstrumentCode);
            var occurrence = baseKeyOccurrences.GetValueOrDefault(baseKey) + 1;
            baseKeyOccurrences[baseKey] = occurrence;
            parsed.Operation.BrokerOperationKey = BrokerOperationKeyBuilder.Build(parsed.Operation, canonicalInstrumentCode, occurrence);
            importedKeys.Add(parsed.Operation.BrokerOperationKey);
        }

        var importedInstrumentIds = parseResult.Operations
            .Where(x => x.Operation.InstrumentId != null)
            .Select(x => x.Operation.InstrumentId!.Value)
            .Distinct()
            .ToArray();

        operationsToReconcile.AddRange(parseResult.Operations.Select(x => x.Operation));

        var existingKeys = importedKeys.Count == 0
            ? new HashSet<string>(StringComparer.Ordinal)
            : (await context.Operations
                .AsNoTracking()
                .Where(x => x.PortfolioId == portfolio.Id && x.BrokerOperationKey != null && importedKeys.Contains(x.BrokerOperationKey))
                .Select(x => x.BrokerOperationKey!)
                .Distinct()
                .ToListAsync(cancellationToken))
            .ToHashSet(StringComparer.Ordinal);

        var manualCandidates = Array.Empty<Operation>();
        if (operationsToReconcile.Count > 0 &&
            BrokerImportReconciliationHelper.SupportsManualReconciliation(portfolio.Broker?.Code))
        {
            var (fromDate, toDate) = BrokerImportReconciliationHelper.GetManualCandidateWindow(operationsToReconcile);
            manualCandidates = await context.Operations
                .Where(x =>
                    x.PortfolioId == portfolio.Id &&
                    (x.BrokerOperationKey == null || x.BrokerOperationKey.StartsWith("manual:v2:")) &&
                    x.TradeDate >= fromDate &&
                    x.TradeDate <= toDate)
                .ToArrayAsync(cancellationToken);
        }

        var reservedManualIds = new HashSet<Guid>();
        DateTime? earliestTouchedDate = null;

        foreach (var operation in operationsToReconcile.OrderBy(x => x.TradeDate).ThenBy(x => x.CreatedAt))
        {
            var brokerOperationKey = operation.BrokerOperationKey!;
            if (!existingKeys.Add(brokerOperationKey))
            {
                skippedCount++;
                continue;
            }

            var manualMatch = BrokerImportReconciliationHelper.TryFindManualMatch(
                portfolio.Broker?.Code,
                operation,
                manualCandidates,
                reservedManualIds);

            if (manualMatch is not null)
            {
                reservedManualIds.Add(manualMatch.Id);
                var originalTradeDate = manualMatch.TradeDate;
                BrokerImportReconciliationHelper.ApplyImportedValues(manualMatch, operation);
                earliestTouchedDate = earliestTouchedDate is null
                    ? MinDate(originalTradeDate, operation.TradeDate)
                    : MinDate(earliestTouchedDate.Value, MinDate(originalTradeDate, operation.TradeDate));
                reconciledCount++;
                continue;
            }

            operationsToInsert.Add(operation);
            earliestTouchedDate = earliestTouchedDate is null
                ? operation.TradeDate
                : MinDate(earliestTouchedDate.Value, operation.TradeDate);
        }

        if (skippedCount > 0 || reconciledCount > 0)
        {
            logger.LogInformation(
                "Broker import: skipped {SkippedCount} duplicates and reconciled {ReconciledCount} manual operations for portfolio {PortfolioId} from file {FileName}",
                skippedCount,
                reconciledCount,
                portfolio.Id,
                request.FileName);
        }

        if (operationsToInsert.Count > 0)
        {
            await context.Operations.AddRangeAsync(operationsToInsert, cancellationToken);
        }

        if (operationsToInsert.Count > 0 || reconciledCount > 0)
        {
            await context.SaveChangesAsync(cancellationToken);
        }

        if (earliestTouchedDate is not null)
        {
            await recalc.ScheduleRebuild(portfolio.Id, earliestTouchedDate.Value, cancellationToken);
        }

        var result = new ImportResultDto(
            ImportedOperations: operationsToInsert.Count,
            SkippedOperations: skippedCount,
            Errors: parseResult.Errors);

        return Result<ImportResultDto>.Success(result);
    }

    private static string NormalizeCode(string value) => value.Trim().ToUpperInvariant();

    private static DateTime EnsureUtc(DateTime value)
    {
        return value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };
    }

    private static DateTime MinDate(DateTime left, DateTime right) => left <= right ? left : right;
}
