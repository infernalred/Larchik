using Larchik.Application.Contracts;
using Larchik.Application.Helpers;
using Larchik.Application.Models;
using Larchik.Application.Portfolios.DailyAttribution;
using Larchik.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Larchik.Application.Portfolios.GetAggregatePortfolioDailyAttribution;

public sealed class GetAggregatePortfolioDailyAttributionQueryHandler(
    LarchikContext context,
    IUserAccessor userAccessor,
    IMemoryCache memoryCache)
{
    public async Task<Result<DailyPnlAttributionDto>> Handle(
        GetAggregatePortfolioDailyAttributionQuery request,
        CancellationToken cancellationToken)
    {
        var userId = userAccessor.GetUserId();
        var portfolios = await context.Portfolios
            .Include(x => x.Broker)
            .Where(x => x.UserId == userId)
            .ToListAsync(cancellationToken);
        if (portfolios.Count == 0)
        {
            return Result<DailyPnlAttributionDto>.Failure("No portfolios found");
        }

        var baseCurrency = PortfolioAnalyticsQueryHelper.ResolveBaseCurrency(request.Currency, portfolios);
        if (baseCurrency is null)
        {
            return Result<DailyPnlAttributionDto>.Failure(
                "Portfolios use different reporting currencies. Specify the 'currency' query parameter.");
        }

        var requestedDate = PortfolioAnalyticsQueryHelper.NormalizeMaxPriceDateUtc(request.Date);
        var portfolioIds = portfolios.Select(x => x.Id).ToArray();
        var operationState = await PortfolioSummaryOperationState.ForPortfoliosAsync(
            context,
            portfolioIds,
            requestedDate,
            cancellationToken);
        var marketFingerprint = await PortfolioSummaryMarketDataFingerprint.ForPortfoliosAsync(
            context,
            portfolioIds,
            baseCurrency,
            requestedDate,
            cancellationToken);
        var cacheKey = DailyAttributionCacheKeys.Aggregate(
            userId,
            portfolioIds,
            baseCurrency,
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
            .Where(x => portfolioIds.Contains(x.PortfolioId) && x.TradeDate <= requestedDate)
            .OrderBy(x => x.PortfolioId)
            .ThenBy(x => x.TradeDate)
            .ThenBy(x => x.CreatedAt)
            .ToListAsync(cancellationToken);
        var pools = await PortfolioAnalyticsQueryHelper.LoadSharedPoolsAsync(
            context,
            operations,
            baseCurrency,
            requestedDate,
            additionalCurrencies: null,
            cancellationToken,
            useNarrowPriceHistory: false);
        var mergedByPortfolio = portfolios.ToDictionary(
            x => x.Id,
            x => InstrumentCorporateActionOperationMerger.Merge(
                    operations.Where(o => o.PortfolioId == x.Id).ToList(),
                    pools.CorporateActions,
                    pools.Instruments)
                .ToList());
        var allMerged = mergedByPortfolio.Values.SelectMany(x => x).ToList();
        var currencies = pools.Instruments.Values
            .Select(x => x.CurrencyId)
            .Concat(allMerged.Select(x => x.CurrencyId))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var heldInstrumentIds = portfolios
            .SelectMany(portfolio => DailyAttributionInstrumentSelector.SelectHeldMarketInstruments(
                portfolio,
                mergedByPortfolio[portfolio.Id],
                pools.Instruments,
                pools.Data,
                baseCurrency,
                requestedDate))
            .Distinct()
            .ToArray();
        var period = DailyAttributionDateResolver.Resolve(
            pools.Data,
            allMerged,
            heldInstrumentIds,
            currencies,
            baseCurrency,
            requestedDate);
        var calculator = new DailyPnlAttributionCalculator();
        var results = portfolios
            .Select(portfolio => calculator.Calculate(
                portfolio,
                mergedByPortfolio[portfolio.Id],
                pools.Instruments,
                pools.Data,
                baseCurrency,
                period.ComparisonDate,
                period.ValuationDate))
            .ToArray();

        var result = Aggregate(results, baseCurrency, period);
        memoryCache.Set(cacheKey, result, PortfolioSummaryCacheKeys.DefaultTtl);
        return Result<DailyPnlAttributionDto>.Success(result);
    }

    private static DailyPnlAttributionDto Aggregate(
        IReadOnlyCollection<DailyPnlAttributionDto> results,
        string baseCurrency,
        DailyAttributionPeriod period)
    {
        var startNavBase = results.Sum(x => x.StartNavBase);
        var pnlBase = results.Sum(x => x.PnlBase);
        var positions = results
            .SelectMany(x => x.Positions)
            .GroupBy(x => x.InstrumentId)
            .Select(group =>
            {
                var sample = group.First();
                var startMarketValueBase = group.Sum(x => x.StartMarketValueBase);
                return sample with
                {
                    StartQuantity = group.Sum(x => x.StartQuantity),
                    EndQuantity = group.Sum(x => x.EndQuantity),
                    StartMarketValueBase = startMarketValueBase,
                    EndMarketValueBase = group.Sum(x => x.EndMarketValueBase),
                    PnlBase = group.Sum(x => x.PnlBase),
                    ReturnPct = startMarketValueBase == 0m ? null : group.Sum(x => x.PnlBase) / startMarketValueBase,
                    PriceEffectBase = group.Sum(x => x.PriceEffectBase),
                    FxEffectBase = group.Sum(x => x.FxEffectBase),
                    CrossEffectBase = group.Sum(x => x.CrossEffectBase),
                    TradingEffectBase = group.Sum(x => x.TradingEffectBase),
                    IncomeEffectBase = group.Sum(x => x.IncomeEffectBase),
                    FeeEffectBase = group.Sum(x => x.FeeEffectBase),
                    OtherEffectBase = group.Sum(x => x.OtherEffectBase),
                    DataQuality = group.All(x => x.DataQuality == "complete") ? "complete" : "partial",
                    Warnings = group.SelectMany(x => x.Warnings).Distinct().ToArray()
                };
            })
            .OrderBy(x => x.PnlBase)
            .ToArray();
        var cash = results
            .SelectMany(x => x.Cash)
            .GroupBy(x => x.CurrencyId, StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var sample = group.First();
                return sample with
                {
                    StartAmount = group.Sum(x => x.StartAmount),
                    EndAmount = group.Sum(x => x.EndAmount),
                    FxEffectBase = group.Sum(x => x.FxEffectBase),
                    DataQuality = group.All(x => x.DataQuality == "complete") ? "complete" : "partial"
                };
            })
            .OrderBy(x => x.CurrencyId)
            .ToArray();

        return new DailyPnlAttributionDto
        {
            PortfolioId = null,
            Name = "Все счета",
            ReportingCurrencyId = baseCurrency,
            ComparisonDate = period.ComparisonDate,
            ValuationDate = period.ValuationDate,
            StartNavBase = startNavBase,
            EndNavBase = results.Sum(x => x.EndNavBase),
            ExternalFlowBase = results.Sum(x => x.ExternalFlowBase),
            PnlBase = pnlBase,
            ReturnPct = startNavBase == 0m ? null : pnlBase / startNavBase,
            PriceEffectBase = results.Sum(x => x.PriceEffectBase),
            SecurityFxEffectBase = results.Sum(x => x.SecurityFxEffectBase),
            CrossEffectBase = results.Sum(x => x.CrossEffectBase),
            TradingEffectBase = results.Sum(x => x.TradingEffectBase),
            CashFxEffectBase = results.Sum(x => x.CashFxEffectBase),
            IncomeEffectBase = results.Sum(x => x.IncomeEffectBase),
            FeeEffectBase = results.Sum(x => x.FeeEffectBase),
            OtherEffectBase = results.Sum(x => x.OtherEffectBase),
            ReconciliationResidualBase = results.Sum(x => x.ReconciliationResidualBase),
            IsComplete = results.All(x => x.IsComplete),
            Warnings = results.SelectMany(x => x.Warnings).Distinct().ToArray(),
            Positions = positions,
            Cash = cash
        };
    }
}
