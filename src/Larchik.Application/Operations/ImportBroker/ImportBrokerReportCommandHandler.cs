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
            .Where(x => x.Id == request.PortfolioId && x.UserId == userId)
            .Select(x => new PortfolioIdentity(x.Id, x.Broker == null ? null : x.Broker.Code))
            .FirstOrDefaultAsync(cancellationToken);

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

        var resolution = await BrokerImportInstrumentResolver.ResolveAsync(
            context,
            parseResult.Operations,
            cancellationToken);

        if (resolution.HasErrors)
        {
            var errors = parseResult.Errors
                .Concat(resolution.BuildErrors())
                .ToArray();
            return Result<ImportResultDto>.Failure(string.Join("; ", errors));
        }

        var operationsToInsert = new List<Operation>(parseResult.Operations.Count);
        var skippedCount = 0;
        var reconciledCount = 0;
        var preparedBatch = BrokerImportBatchBuilder.Prepare(parseResult.Operations, portfolio.Id, resolution);
        var operationsToReconcile = preparedBatch.Operations;

        var existingKeys = preparedBatch.ImportedKeys.Count == 0
            ? new HashSet<string>(StringComparer.Ordinal)
            : (await context.Operations
                .Where(x =>
                    x.PortfolioId == portfolio.Id &&
                    x.BrokerOperationKey != null &&
                    preparedBatch.ImportedKeys.Contains(x.BrokerOperationKey))
                .Select(x => x.BrokerOperationKey!)
                .Distinct()
                .ToListAsync(cancellationToken))
            .ToHashSet(StringComparer.Ordinal);

        var manualCandidates = Array.Empty<Operation>();
        if (operationsToReconcile.Count > 0 &&
            BrokerImportReconciliationHelper.SupportsManualReconciliation(portfolio.BrokerCode))
        {
            var (fromDate, toDate) = BrokerImportReconciliationHelper.GetManualCandidateWindow(operationsToReconcile);
            manualCandidates = await context.Operations
                .AsTracking()
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
                portfolio.BrokerCode,
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

        if (operationsToInsert.Count > 0 || reconciledCount > 0)
        {
            await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
            if (operationsToInsert.Count > 0)
            {
                await context.Operations.AddRangeAsync(operationsToInsert, cancellationToken);
            }

            await context.SaveChangesAsync(cancellationToken);

            if (earliestTouchedDate is not null)
            {
                await recalc.ScheduleRebuild(portfolio.Id, earliestTouchedDate.Value, cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
        }

        var result = new ImportResultDto(
            ImportedOperations: operationsToInsert.Count,
            SkippedOperations: skippedCount,
            Errors: parseResult.Errors);

        return Result<ImportResultDto>.Success(result);
    }
    private static DateTime MinDate(DateTime left, DateTime right) => left <= right ? left : right;

    private sealed record PortfolioIdentity(Guid Id, string? BrokerCode);
}
