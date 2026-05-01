using Larchik.Application.Contracts;
using Larchik.Application.Helpers;
using Larchik.Application.Models;
using Larchik.Persistence.Context;
using Larchik.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Larchik.Application.Operations.ImportBroker;

public class ImportBrokerReportCommandHandler(
    LarchikContext context,
    IUserAccessor userAccessor,
    IPortfolioRecalcService recalc,
    IEnumerable<IBrokerReportParser> parsers,
    ILogger<ImportBrokerReportCommandHandler> logger)
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
            "Broker import: parser {ParserCode} returned {OperationCount} operations, {ErrorCount} errors and {WarningCount} warnings for file {FileName}",
            parser.Code,
            parseResult.Operations.Count,
            parseResult.Errors.Count,
            parseResult.Warnings?.Count ?? 0,
            request.FileName);

        if (request.StrictUnknownCashMapping &&
            parseResult.Warnings?.Count > 0 &&
            parseResult.Warnings.Any(w => w.StartsWith("Cash operation fallback to CashAdjustment", StringComparison.OrdinalIgnoreCase)))
        {
            return Result<ImportResultDto>.Failure(
                string.Join("; ", parseResult.Warnings));
        }

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
                    (x.BrokerOperationKey == null ||
                     x.BrokerOperationKey.StartsWith("manual:v2:") ||
                     x.BrokerOperationKey.StartsWith("manual:v3:")) &&
                    x.TradeDate >= fromDate &&
                    x.TradeDate <= toDate)
                .ToArrayAsync(cancellationToken);
        }

        // Compatibility path: when a broker file is reimported after we switched from v2: keys to v3: keys,
        // there can be already persisted confirmed rows with v2 prefix. Those rows must be treated as duplicates
        // based on economic identity (base hash), not on exact brokerOperationKey string.
        var existingConfirmedBaseHashCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        static string BuildCompatibilityBaseHash(Operation op)
        {
            // v2->v3 migration may reparse cash amounts after we've fixed T-Bank Excel artifacts.
            // To avoid missing duplicates due to tiny float drifts in cash rows,
            // normalize cash amounts to currency precision (2 decimals) before building the base hash.
            var type = op.Type;
            var normalized = type is OperationType.Deposit or OperationType.Withdraw or OperationType.Fee or
                OperationType.CashAdjustment or OperationType.Dividend
                ? new Operation
                {
                    Type = op.Type,
                    InstrumentId = op.InstrumentId,
                    Quantity = decimal.Round(op.Quantity, 6, MidpointRounding.AwayFromZero),
                    Price = decimal.Round(op.Price, 2, MidpointRounding.AwayFromZero),
                    Fee = decimal.Round(op.Fee, 4, MidpointRounding.AwayFromZero),
                    CurrencyId = op.CurrencyId,
                    TradeDate = op.TradeDate,
                    SettlementDate = op.SettlementDate,
                }
                : new Operation
                {
                    Type = op.Type,
                    InstrumentId = op.InstrumentId,
                    Quantity = decimal.Round(op.Quantity, 6, MidpointRounding.AwayFromZero),
                    Price = op.Price,
                    Fee = decimal.Round(op.Fee, 4, MidpointRounding.AwayFromZero),
                    CurrencyId = op.CurrencyId,
                    TradeDate = op.TradeDate,
                    SettlementDate = op.SettlementDate,
                };

            return BrokerOperationKeyBuilder.BuildBaseHash(normalized, null);
        }

        if (operationsToReconcile.Count > 0)
        {
            var fromDate = operationsToReconcile.Min(x => x.TradeDate).Date;
            var toDate = operationsToReconcile.Max(x => x.TradeDate).Date;

            var targetBaseHashes = operationsToReconcile
                .Select(BuildCompatibilityBaseHash)
                .ToHashSet(StringComparer.Ordinal);

            var confirmedCandidates = await context.Operations
                .Where(x =>
                    x.PortfolioId == portfolio.Id &&
                    x.BrokerOperationKey != null &&
                    // Compatibility map is only needed for legacy v2: rows.
                    // Exact-key v3: duplicates are already skipped via `existingKeys.Add(...)` earlier.
                    x.BrokerOperationKey.StartsWith("v2:") &&
                    x.TradeDate >= fromDate &&
                    x.TradeDate < toDate.AddDays(1))
                .Select(x => new
                {
                    x.Type,
                    x.InstrumentId,
                    x.Quantity,
                    x.Price,
                    x.Fee,
                    x.CurrencyId,
                    x.TradeDate,
                    x.SettlementDate
                })
                .ToListAsync(cancellationToken);

            foreach (var candidate in confirmedCandidates)
            {
                var op = new Operation
                {
                    Type = candidate.Type,
                    InstrumentId = candidate.InstrumentId,
                    Quantity = candidate.Quantity,
                    Price = candidate.Price,
                    Fee = candidate.Fee,
                    CurrencyId = candidate.CurrencyId,
                    TradeDate = candidate.TradeDate,
                    SettlementDate = candidate.SettlementDate
                };

                var baseHash = BuildCompatibilityBaseHash(op);
                if (!targetBaseHashes.Contains(baseHash))
                {
                    continue;
                }

                existingConfirmedBaseHashCounts.TryGetValue(baseHash, out var count);
                existingConfirmedBaseHashCounts[baseHash] = count + 1;
            }
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

            // If there's already a confirmed legacy v2 row with the same economic identity,
            // skip importing (do this before manual reconciliation to avoid producing duplicates).
            var importedBaseHash = BuildCompatibilityBaseHash(operation);
            if (existingConfirmedBaseHashCounts.TryGetValue(importedBaseHash, out var baseCount) && baseCount > 0)
            {
                existingConfirmedBaseHashCounts[importedBaseHash] = baseCount - 1;
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
            Errors: parseResult.Errors,
            Warnings: parseResult.Warnings ?? []);

        return Result<ImportResultDto>.Success(result);
    }
    private static DateTime MinDate(DateTime left, DateTime right) => left <= right ? left : right;


    private sealed record PortfolioIdentity(Guid Id, string? BrokerCode);
}
