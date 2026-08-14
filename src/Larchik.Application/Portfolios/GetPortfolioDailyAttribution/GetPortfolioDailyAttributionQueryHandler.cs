using Larchik.Application.Contracts;
using Larchik.Application.Helpers;
using Larchik.Application.Models;
using Larchik.Application.Portfolios.DailyAttribution;
using Larchik.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Larchik.Application.Portfolios.GetPortfolioDailyAttribution;

public sealed class GetPortfolioDailyAttributionQueryHandler(
    LarchikContext context,
    IUserAccessor userAccessor,
    IMemoryCache memoryCache)
{
    public async Task<Result<DailyPnlAttributionDto>?> Handle(
        GetPortfolioDailyAttributionQuery request,
        CancellationToken cancellationToken)
    {
        var userId = userAccessor.GetUserId();
        var portfolio = await context.Portfolios
            .Include(x => x.Broker)
            .FirstOrDefaultAsync(x => x.Id == request.Id && x.UserId == userId, cancellationToken);
        if (portfolio is null)
        {
            return null;
        }

        var requestedDate = PortfolioAnalyticsQueryHelper.NormalizeMaxPriceDateUtc(request.Date);
        var operationState = await PortfolioSummaryOperationState.ForPortfolioAsync(
            context,
            request.Id,
            requestedDate,
            cancellationToken);
        var marketFingerprint = await PortfolioSummaryMarketDataFingerprint.ForPortfolioAsync(
            context,
            request.Id,
            portfolio.ReportingCurrencyId,
            requestedDate,
            cancellationToken);
        var cacheKey = DailyAttributionCacheKeys.Single(
            userId,
            request.Id,
            requestedDate,
            operationState.Count,
            operationState.MaxCreatedTicks,
            operationState.MaxUpdatedTicks,
            marketFingerprint.MaxPriceDataTicks,
            marketFingerprint.MaxFxRateDataTicks);
        if (memoryCache.TryGetValue(cacheKey, out DailyPnlAttributionDto? cached) && cached is not null)
        {
            return Result<DailyPnlAttributionDto>.Success(cached);
        }

        var operations = await context.Operations
            .Where(x => x.PortfolioId == request.Id && x.TradeDate <= requestedDate)
            .OrderBy(x => x.TradeDate)
            .ThenBy(x => x.CreatedAt)
            .ToListAsync(cancellationToken);
        var analytics = await PortfolioAnalyticsQueryHelper.LoadAsync(
            context,
            operations,
            portfolio.ReportingCurrencyId,
            requestedDate,
            additionalCurrencies: null,
            cancellationToken,
            useNarrowPriceHistory: false);
        var currencies = analytics.Instruments.Values
            .Select(x => x.CurrencyId)
            .Concat(analytics.Operations.Select(x => x.CurrencyId))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var period = DailyAttributionDateResolver.Resolve(
            analytics.Data,
            analytics.Operations,
            analytics.Instruments.Keys.ToArray(),
            currencies,
            portfolio.ReportingCurrencyId,
            requestedDate);
        var result = new DailyPnlAttributionCalculator().Calculate(
            portfolio,
            analytics.Operations,
            analytics.Instruments,
            analytics.Data,
            portfolio.ReportingCurrencyId,
            period.ComparisonDate,
            period.ValuationDate);

        memoryCache.Set(cacheKey, result, PortfolioSummaryCacheKeys.DefaultTtl);

        return Result<DailyPnlAttributionDto>.Success(result);
    }
}
