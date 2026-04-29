using Larchik.Application.Portfolios;
using Larchik.Application.Portfolios.Reconciliation;
using Larchik.Persistence.Context;
using Larchik.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PersistedReconciliationResult = Larchik.Persistence.Entities.PortfolioReconciliationResult;
using ReconciliationOutcome = Larchik.Application.Portfolios.Reconciliation.PortfolioReconciliationResult;

namespace Larchik.Infrastructure.Jobs;

public sealed class PortfolioReconciliationReportService(
    LarchikContext context,
    IOptionsMonitor<BackgroundJobsOptions> optionsMonitor,
    ILogger<PortfolioReconciliationReportService> logger)
    : IPortfolioReconciliationReportService
{
    public async Task LogDailyReportAsync(DateOnly runDate, string source, CancellationToken cancellationToken)
    {
        var options = optionsMonitor.CurrentValue.PortfolioReconciliationDaily;
        if (!options.Enabled || options.Targets.Length == 0)
        {
            return;
        }

        var defaultTolerance = options.DeltaToleranceBase < 0 ? 0 : options.DeltaToleranceBase;
        var warningMultiplier = options.WarningToleranceMultiplier <= 0 ? 1m : options.WarningToleranceMultiplier;
        var criticalMultiplier = options.CriticalToleranceMultiplier < warningMultiplier
            ? warningMultiplier
            : options.CriticalToleranceMultiplier;
        var dayUtc = DateTime.SpecifyKind(runDate.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
        var asOfDateUtc = dayUtc.AddDays(1).AddTicks(-1);
        var targets = options.Targets
            .Where(target => ReconciliationTargetDateHelper.ShouldIncludeTarget(target, runDate))
            .ToArray();
        if (targets.Length == 0)
        {
            return;
        }

        var targetPortfolioIds = targets.Select(x => x.PortfolioId).Distinct().ToArray();
        var targetCurrenciesByPortfolio = targets
            .GroupBy(x => x.PortfolioId)
            .ToDictionary(
                x => x.Key,
                x => x
                    .Select(y => string.IsNullOrWhiteSpace(y.CurrencyId) ? null : y.CurrencyId!.Trim().ToUpperInvariant())
                    .Where(y => !string.IsNullOrWhiteSpace(y))
                    .Cast<string>()
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray());
        var portfolios = await context.Portfolios
            .Where(x => targetPortfolioIds.Contains(x.Id))
            .Select(x => new { x.Id, x.Name, x.ReportingCurrencyId, BrokerCode = x.Broker != null ? x.Broker.Code : null })
            .ToDictionaryAsync(x => x.Id, cancellationToken);

        var operationsByPortfolio = await context.Operations
            .Where(x => targetPortfolioIds.Contains(x.PortfolioId) && x.TradeDate <= asOfDateUtc)
            .OrderBy(x => x.TradeDate)
            .ThenBy(x => x.CreatedAt)
            .GroupBy(x => x.PortfolioId)
            .ToDictionaryAsync(x => x.Key, x => (IReadOnlyList<Operation>)x.ToList(), cancellationToken);
        var calculator = new PortfolioAnalyticsCalculator();
        var persistedResults = new List<PersistedReconciliationResult>();

        foreach (var target in targets)
        {
            if (!portfolios.TryGetValue(target.PortfolioId, out var portfolio))
            {
                logger.LogWarning(
                    "Reconciliation target portfolio not found. Source: {Source}. PortfolioId: {PortfolioId}",
                    source,
                    target.PortfolioId);
                continue;
            }

            if (!operationsByPortfolio.TryGetValue(target.PortfolioId, out var operations) || operations.Count == 0)
            {
                persistedResults.Add(CreatePersistedResult(
                    target.PortfolioId,
                    dayUtc,
                    source,
                    portfolio.ReportingCurrencyId,
                    status: "skipped",
                    severity: "warning",
                    alertRequired: true,
                    reasonCode: "no_operations",
                    tolerance: target.DeltaToleranceBase ?? defaultTolerance));
                logger.LogWarning(
                    "Reconciliation skipped because no operations were found. Source: {Source}. Portfolio: {PortfolioName} ({PortfolioId}), date: {Date}",
                    source,
                    portfolio.Name,
                    target.PortfolioId,
                    runDate.ToString("yyyy-MM-dd"));
                continue;
            }

            var analytics = await PortfolioAnalyticsQueryHelper.LoadAsync(
                context,
                operations,
                portfolio.ReportingCurrencyId,
                asOfDateUtc,
                targetCurrenciesByPortfolio.GetValueOrDefault(target.PortfolioId) ?? [],
                cancellationToken);
            var summary = calculator.CalculateSummary(
                new Portfolio
                {
                    Id = target.PortfolioId,
                    Name = portfolio.Name,
                    ReportingCurrencyId = portfolio.ReportingCurrencyId,
                    Broker = string.IsNullOrWhiteSpace(portfolio.BrokerCode)
                        ? null
                        : new Broker { Code = portfolio.BrokerCode }
                },
                analytics.Operations,
                analytics.Instruments,
                analytics.Data,
                valuationMethod: "adjustingAvg",
                baseCurrency: portfolio.ReportingCurrencyId,
                asOfDate: asOfDateUtc);

            var targetCurrency = string.IsNullOrWhiteSpace(target.CurrencyId)
                ? portfolio.ReportingCurrencyId
                : target.CurrencyId.Trim().ToUpperInvariant();
            if (!string.Equals(targetCurrency, portfolio.ReportingCurrencyId, StringComparison.OrdinalIgnoreCase))
            {
                logger.LogInformation(
                    "Reconciliation target currency conversion applied. Source: {Source}. Portfolio: {PortfolioName} ({PortfolioId}). " +
                    "Date: {Date}. Target currency: {TargetCurrency}, reporting currency: {ReportingCurrency}",
                    source,
                    portfolio.Name,
                    target.PortfolioId,
                    runDate.ToString("yyyy-MM-dd"),
                    targetCurrency,
                    portfolio.ReportingCurrencyId);
            }

            if (!string.Equals(targetCurrency, portfolio.ReportingCurrencyId, StringComparison.OrdinalIgnoreCase) &&
                analytics.Data.GetRate(targetCurrency, portfolio.ReportingCurrencyId, asOfDateUtc) is null)
            {
                persistedResults.Add(CreatePersistedResult(
                    target.PortfolioId,
                    dayUtc,
                    source,
                    portfolio.ReportingCurrencyId,
                    status: "skipped",
                    severity: "warning",
                    alertRequired: true,
                    reasonCode: "missing_fx_rate",
                    tolerance: target.DeltaToleranceBase ?? defaultTolerance,
                    actualNavBase: summary.NavBase,
                    actualCashBase: summary.CashBase,
                    actualPositionsValueBase: summary.PositionsValueBase));
                logger.LogWarning(
                    "Reconciliation skipped because FX rate is missing for target currency conversion. Source: {Source}. " +
                    "Portfolio: {PortfolioName} ({PortfolioId}). Date: {Date}. Target currency: {TargetCurrency}, reporting currency: {ReportingCurrency}",
                    source,
                    portfolio.Name,
                    target.PortfolioId,
                    runDate.ToString("yyyy-MM-dd"),
                    targetCurrency,
                    portfolio.ReportingCurrencyId);
                continue;
            }

            var statement = new BrokerageStatementSnapshot(
                dayUtc,
                analytics.Data.Convert(target.NavBase, targetCurrency, portfolio.ReportingCurrencyId, asOfDateUtc),
                analytics.Data.Convert(target.CashBase, targetCurrency, portfolio.ReportingCurrencyId, asOfDateUtc),
                analytics.Data.Convert(target.PositionsValueBase, targetCurrency, portfolio.ReportingCurrencyId, asOfDateUtc));
            var tolerance = target.DeltaToleranceBase ?? defaultTolerance;
            var result = PortfolioReconciliationHelper.Compare(summary, statement, tolerance);
            var severity = DetermineSeverity(result, tolerance, warningMultiplier, criticalMultiplier);
            var alertRequired = severity is "warning" or "critical";
            persistedResults.Add(CreatePersistedResult(
                target.PortfolioId,
                dayUtc,
                source,
                portfolio.ReportingCurrencyId,
                status: result.IsWithinTolerance ? "matched" : "mismatch",
                severity: severity,
                alertRequired: alertRequired,
                reasonCode: result.IsWithinTolerance ? "within_tolerance" : "delta_exceeds_tolerance",
                tolerance: tolerance,
                actualNavBase: summary.NavBase,
                actualCashBase: summary.CashBase,
                actualPositionsValueBase: summary.PositionsValueBase,
                targetNavBase: statement.NavBase,
                targetCashBase: statement.CashBase,
                targetPositionsValueBase: statement.PositionsValueBase,
                navDelta: result.NavDelta,
                cashDelta: result.CashDelta,
                positionsDelta: result.PositionsDelta));

            if (result.IsWithinTolerance)
            {
                logger.LogInformation(
                    "Portfolio reconciliation is within tolerance. Source: {Source}. Portfolio: {PortfolioName} ({PortfolioId}). " +
                    "Date: {Date}, tolerance: {Tolerance}, nav delta: {NavDelta}, cash delta: {CashDelta}, positions delta: {PositionsDelta}",
                    source,
                    portfolio.Name,
                    target.PortfolioId,
                    runDate.ToString("yyyy-MM-dd"),
                    tolerance,
                    result.NavDelta,
                    result.CashDelta,
                    result.PositionsDelta);
            }
            else
            {
                if (severity == "critical")
                {
                    logger.LogError(
                        "Portfolio reconciliation CRITICAL mismatch. Source: {Source}. Portfolio: {PortfolioName} ({PortfolioId}). " +
                        "Date: {Date}, tolerance: {Tolerance}, nav delta: {NavDelta}, cash delta: {CashDelta}, positions delta: {PositionsDelta}",
                        source,
                        portfolio.Name,
                        target.PortfolioId,
                        runDate.ToString("yyyy-MM-dd"),
                        tolerance,
                        result.NavDelta,
                        result.CashDelta,
                        result.PositionsDelta);
                }
                else
                {
                    logger.LogWarning(
                        "Portfolio reconciliation mismatch. Source: {Source}. Portfolio: {PortfolioName} ({PortfolioId}). " +
                        "Date: {Date}, tolerance: {Tolerance}, nav delta: {NavDelta}, cash delta: {CashDelta}, positions delta: {PositionsDelta}",
                        source,
                        portfolio.Name,
                        target.PortfolioId,
                        runDate.ToString("yyyy-MM-dd"),
                        tolerance,
                        result.NavDelta,
                        result.CashDelta,
                        result.PositionsDelta);
                }
            }
        }

        if (persistedResults.Count > 0)
        {
            await context.PortfolioReconciliationResults.AddRangeAsync(persistedResults, cancellationToken);
            await context.SaveChangesAsync(cancellationToken);
        }
    }

    private static PersistedReconciliationResult CreatePersistedResult(
        Guid portfolioId,
        DateTime statementDateUtc,
        string source,
        string reportingCurrencyId,
        string status,
        string severity,
        bool alertRequired,
        string reasonCode,
        decimal tolerance,
        decimal actualNavBase = 0m,
        decimal actualCashBase = 0m,
        decimal actualPositionsValueBase = 0m,
        decimal targetNavBase = 0m,
        decimal targetCashBase = 0m,
        decimal targetPositionsValueBase = 0m,
        decimal navDelta = 0m,
        decimal cashDelta = 0m,
        decimal positionsDelta = 0m) =>
        new()
        {
            Id = Guid.NewGuid(),
            PortfolioId = portfolioId,
            StatementDate = statementDateUtc,
            Source = source,
            ReportingCurrencyId = reportingCurrencyId,
            Status = status,
            Severity = severity,
            AlertRequired = alertRequired,
            ReasonCode = reasonCode,
            ToleranceBase = tolerance,
            ActualNavBase = actualNavBase,
            ActualCashBase = actualCashBase,
            ActualPositionsValueBase = actualPositionsValueBase,
            TargetNavBase = targetNavBase,
            TargetCashBase = targetCashBase,
            TargetPositionsValueBase = targetPositionsValueBase,
            NavDelta = navDelta,
            CashDelta = cashDelta,
            PositionsDelta = positionsDelta,
            CreatedAt = DateTime.UtcNow
        };

    private static string DetermineSeverity(
        ReconciliationOutcome result,
        decimal tolerance,
        decimal warningMultiplier,
        decimal criticalMultiplier)
    {
        if (result.IsWithinTolerance)
        {
            return "info";
        }

        var maxAbsDelta = Math.Max(Math.Abs(result.NavDelta), Math.Max(Math.Abs(result.CashDelta), Math.Abs(result.PositionsDelta)));
        var warningThreshold = tolerance * warningMultiplier;
        var criticalThreshold = tolerance * criticalMultiplier;
        if (maxAbsDelta >= criticalThreshold)
        {
            return "critical";
        }

        return maxAbsDelta >= warningThreshold ? "warning" : "warning";
    }

}
