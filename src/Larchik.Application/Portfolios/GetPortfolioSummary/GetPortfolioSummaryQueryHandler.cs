using Larchik.Application.Contracts;
using Larchik.Application.Helpers;
using Larchik.Application.Models;
using Larchik.Application.Portfolios.DailyAttribution;
using Larchik.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Larchik.Application.Portfolios.GetPortfolioSummary;

public class GetPortfolioSummaryQueryHandler(
    LarchikContext context,
    IUserAccessor userAccessor,
    IMemoryCache memoryCache)
{
    private const string DefaultValuationMethod = "adjustingAvg";

    public async Task<Result<PortfolioSummaryDto>> Handle(GetPortfolioSummaryQuery request, CancellationToken cancellationToken)
    {
        var userId = userAccessor.GetUserId();
        var method = request.Method ?? DefaultValuationMethod;

        var portfolio = await context.Portfolios
            .Include(x => x.Broker)
            .FirstOrDefaultAsync(x => x.Id == request.Id && x.UserId == userId, cancellationToken);

        if (portfolio is null)
        {
            return null!;
        }

        var asOfDateTime = DateTime.UtcNow;
        var opState = await PortfolioSummaryOperationState.ForPortfolioAsync(context, request.Id, asOfDateTime, cancellationToken);
        var marketFp = await PortfolioSummaryMarketDataFingerprint.ForPortfolioAsync(
            context,
            request.Id,
            portfolio.ReportingCurrencyId,
            asOfDateTime,
            cancellationToken);
        var cacheKey = PortfolioSummaryCacheKeys.Single(
            userId,
            request.Id,
            method,
            opState.Count,
            opState.MaxCreatedTicks,
            opState.MaxUpdatedTicks,
            marketFp.MaxPriceDataTicks,
            marketFp.MaxFxRateDataTicks);
        if (memoryCache.TryGetValue(cacheKey, out PortfolioSummaryDto? cached) && cached is not null)
        {
            return Result<PortfolioSummaryDto>.Success(cached);
        }

        var operations = await context.Operations
            .Where(x => x.PortfolioId == request.Id && x.TradeDate <= asOfDateTime)
            .OrderBy(x => x.TradeDate)
            .ThenBy(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

        var analytics = await PortfolioAnalyticsQueryHelper.LoadAsync(
            context,
            operations,
            portfolio.ReportingCurrencyId,
            asOfDateTime,
            additionalCurrencies: null,
            cancellationToken,
            useNarrowPriceHistory: false);

        var fromSnapshot = await PortfolioSnapshotSummaryBuilder.TryBuildAsync(
            context,
            portfolio,
            analytics.Operations,
            analytics.Instruments,
            analytics.Data,
            method,
            portfolio.ReportingCurrencyId,
            asOfDateTime,
            includeAnnualizedReturn: true,
            cancellationToken);

        var summary = fromSnapshot ?? new PortfolioAnalyticsCalculator().CalculateSummary(
            portfolio,
            analytics.Operations,
            analytics.Instruments,
            analytics.Data,
            method,
            portfolio.ReportingCurrencyId,
            asOfDateTime,
            includeAnnualizedReturn: true);

        var currencies = analytics.Instruments.Values
            .Select(x => x.CurrencyId)
            .Concat(analytics.Operations.Select(x => x.CurrencyId))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var heldInstrumentIds = DailyAttributionInstrumentSelector.SelectHeldMarketInstruments(
            portfolio,
            analytics.Operations,
            analytics.Instruments,
            analytics.Data,
            portfolio.ReportingCurrencyId,
            asOfDateTime);
        var period = DailyAttributionDateResolver.Resolve(
            analytics.Data,
            analytics.Operations,
            heldInstrumentIds,
            currencies,
            portfolio.ReportingCurrencyId,
            asOfDateTime);
        var attribution = new DailyPnlAttributionCalculator().Calculate(
            portfolio,
            analytics.Operations,
            analytics.Instruments,
            analytics.Data,
            portfolio.ReportingCurrencyId,
            period.ComparisonDate,
            period.ValuationDate);
        DailyAttributionSummaryMapper.Attach(summary, attribution);

        memoryCache.Set(cacheKey, summary, PortfolioSummaryCacheKeys.DefaultTtl);
        return Result<PortfolioSummaryDto>.Success(summary);
    }
}
