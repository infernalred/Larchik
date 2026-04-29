using Larchik.Application.Portfolios;
using Larchik.Application.Portfolios.Reconciliation;
using Larchik.Persistence.Context;
using Larchik.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Larchik.Infrastructure.Jobs;

public sealed class PortfolioReconciliationReportService(
    LarchikContext context,
    IOptionsMonitor<BackgroundJobsOptions> optionsMonitor,
    ILogger<PortfolioReconciliationReportService> logger)
{
    public async Task LogDailyReportAsync(DateOnly runDate, string source, CancellationToken cancellationToken)
    {
        var options = optionsMonitor.CurrentValue.PortfolioReconciliationDaily;
        if (!options.Enabled || options.Targets.Length == 0)
        {
            return;
        }

        var defaultTolerance = options.DeltaToleranceBase < 0 ? 0 : options.DeltaToleranceBase;
        var dayUtc = DateTime.SpecifyKind(runDate.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
        var asOfDateUtc = dayUtc.AddDays(1).AddTicks(-1);
        var targets = options.Targets
            .Where(target => ShouldIncludeTarget(target, runDate))
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

    private static bool ShouldIncludeTarget(PortfolioReconciliationTargetOptions target, DateOnly runDate)
    {
        if (string.IsNullOrWhiteSpace(target.Date))
        {
            return true;
        }

        return DateOnly.TryParse(target.Date, out var targetDate) && targetDate == runDate;
    }

}
