using Larchik.Application.Contracts;
using Larchik.Application.Helpers;
using Larchik.Application.Models;
using Larchik.Application.Portfolios.DailyAttribution;
using Larchik.Persistence.Context;
using Larchik.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Larchik.Application.Portfolios.GetAggregatePortfolioSummary;

public class GetAggregatePortfolioSummaryQueryHandler(
    LarchikContext context,
    IUserAccessor userAccessor,
    IMemoryCache memoryCache)
{
    private const string DefaultValuationMethod = "adjustingAvg";

    public async Task<Result<PortfolioSummaryDto>> Handle(
        GetAggregatePortfolioSummaryQuery request,
        CancellationToken cancellationToken)
    {
        var userId = userAccessor.GetUserId();
        var portfolios = await context.Portfolios
            .Include(x => x.Broker)
            .Where(x => x.UserId == userId)
            .ToListAsync(cancellationToken);

        if (portfolios.Count == 0)
        {
            return Result<PortfolioSummaryDto>.Failure("No portfolios found");
        }

        var baseCurrency = PortfolioAnalyticsQueryHelper.ResolveBaseCurrency(request.Currency, portfolios);
        if (baseCurrency is null)
        {
            return Result<PortfolioSummaryDto>.Failure(
                "Portfolios use different reporting currencies. Specify the 'currency' query parameter.");
        }

        var method = request.Method ?? DefaultValuationMethod;
        var portfolioIds = portfolios.Select(x => x.Id).ToArray();

        var asOfDateTime = DateTime.UtcNow;
        var opState = await PortfolioSummaryOperationState.ForPortfoliosAsync(context, portfolioIds, asOfDateTime, cancellationToken);
        var marketFp = await PortfolioSummaryMarketDataFingerprint.ForPortfoliosAsync(
            context,
            portfolioIds,
            baseCurrency,
            asOfDateTime,
            cancellationToken);
        var cacheKey = PortfolioSummaryCacheKeys.Aggregate(
            userId,
            method,
            baseCurrency,
            portfolioIds,
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
            .Where(x => portfolioIds.Contains(x.PortfolioId) && x.TradeDate <= asOfDateTime)
            .OrderBy(x => x.PortfolioId)
            .ThenBy(x => x.TradeDate)
            .ThenBy(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

        var calculator = new PortfolioAnalyticsCalculator();
        var pools = await PortfolioAnalyticsQueryHelper.LoadSharedPoolsAsync(
            context,
            operations,
            baseCurrency,
            asOfDateTime,
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
                asOfDateTime))
            .Distinct()
            .ToArray();
        var dailyPeriod = DailyAttributionDateResolver.Resolve(
            pools.Data,
            allMerged,
            heldInstrumentIds,
            currencies,
            baseCurrency,
            asOfDateTime);
        var dailyCalculator = new DailyPnlAttributionCalculator();

        var summaries = new List<PortfolioSummaryDto>(portfolios.Count);
        var allMergedForMwr = new List<Operation>();

        foreach (var portfolio in portfolios)
        {
            var merged = mergedByPortfolio[portfolio.Id];
            allMergedForMwr.AddRange(merged);

            var fromSnapshot = await PortfolioSnapshotSummaryBuilder.TryBuildAsync(
                context,
                portfolio,
                merged,
                pools.Instruments,
                pools.Data,
                method,
                baseCurrency,
                asOfDateTime,
                includeAnnualizedReturn: false,
                cancellationToken);

            var summary = fromSnapshot ?? calculator.CalculateSummary(
                portfolio,
                merged,
                pools.Instruments,
                pools.Data,
                method,
                baseCurrency,
                asOfDateTime,
                includeAnnualizedReturn: false);
            var attribution = dailyCalculator.Calculate(
                portfolio,
                merged,
                pools.Instruments,
                pools.Data,
                baseCurrency,
                dailyPeriod.ComparisonDate,
                dailyPeriod.ValuationDate);
            DailyAttributionSummaryMapper.Attach(summary, attribution);
            summaries.Add(summary);
        }

        allMergedForMwr.Sort(static (a, b) =>
        {
            var c = a.TradeDate.CompareTo(b.TradeDate);
            return c != 0 ? c : a.CreatedAt.CompareTo(b.CreatedAt);
        });

        var cash = summaries
            .SelectMany(x => x.Cash)
            .GroupBy(x => x.CurrencyId, StringComparer.OrdinalIgnoreCase)
            .Select(x => new CashBalanceDto
            {
                CurrencyId = x.Key.ToUpperInvariant(),
                Amount = x.Sum(y => y.Amount),
                AmountInBase = x.Sum(y => y.AmountInBase),
                DailyMove = DailyAttributionSummaryMapper.Aggregate(x.Select(y => y.DailyMove))
            })
            .OrderByDescending(x => x.AmountInBase)
            .ToList();

        var positions = summaries
            .SelectMany(x => x.Positions)
            .GroupBy(x => x.InstrumentId)
            .Select(group =>
            {
                var first = group.First();
                var totalQuantity = group.Sum(x => x.Quantity);
                var weightedCost = totalQuantity != 0
                    ? group.Sum(x => x.AverageCost * x.Quantity) / totalQuantity
                    : group.Average(x => x.AverageCost);

                return new PositionHoldingDto
                {
                    InstrumentId = first.InstrumentId,
                    InstrumentName = first.InstrumentName,
                    InstrumentType = first.InstrumentType,
                    CategoryName = first.CategoryName,
                    CurrencyId = first.CurrencyId,
                    PriceCurrencyId = first.PriceCurrencyId,
                    AverageCostCurrencyId = first.AverageCostCurrencyId,
                    Quantity = totalQuantity,
                    LastPrice = group.Select(x => x.LastPrice).FirstOrDefault(x => x.HasValue),
                    MarketValueBase = group.Sum(x => x.MarketValueBase),
                    AverageCost = weightedCost,
                    DailyMove = DailyAttributionSummaryMapper.Aggregate(group.Select(x => x.DailyMove))
                };
            })
            .ToList();
        PositionHoldingSortHelper.SortByAssetClass(positions);

        var realized = summaries
            .SelectMany(x => x.RealizedByInstrument)
            .GroupBy(x => x.InstrumentId)
            .Select(group =>
            {
                var first = group.First();
                return new RealizedPnlDto
                {
                    InstrumentId = first.InstrumentId,
                    InstrumentName = first.InstrumentName,
                    CurrencyId = first.CurrencyId,
                    Realized = group.Sum(x => x.Realized),
                    RealizedBase = group.Sum(x => x.RealizedBase)
                };
            })
            .OrderByDescending(x => Math.Abs(x.RealizedBase))
            .ToList();

        var navBase = summaries.Sum(x => x.NavBase);
        var annualizedReturnPct = MoneyWeightedReturnCalculator.CalculateAnnualizedReturn(
            allMergedForMwr,
            pools.Data,
            baseCurrency,
            navBase,
            asOfDateTime);

        var dto = new PortfolioSummaryDto
        {
            Id = Guid.Empty,
            Name = "Все счета",
            ReportingCurrencyId = baseCurrency,
            NetInflowBase = summaries.Sum(x => x.NetInflowBase),
            GrossDepositsBase = summaries.Sum(x => x.GrossDepositsBase),
            GrossWithdrawalsBase = summaries.Sum(x => x.GrossWithdrawalsBase),
            CashBase = summaries.Sum(x => x.CashBase),
            PositionsValueBase = summaries.Sum(x => x.PositionsValueBase),
            RealizedBase = summaries.Sum(x => x.RealizedBase),
            UnrealizedBase = summaries.Sum(x => x.UnrealizedBase),
            PnlBase = summaries.Sum(x => x.PnlBase),
            AnnualizedReturnPct = annualizedReturnPct,
            NavBase = navBase,
            ValuationMethod = method,
            DailyMove = DailyAttributionSummaryMapper.AggregatePortfolios(summaries.Select(x => x.DailyMove)),
            Cash = cash,
            Positions = positions,
            RealizedByInstrument = realized
        };

        memoryCache.Set(cacheKey, dto, PortfolioSummaryCacheKeys.DefaultTtl);
        return Result<PortfolioSummaryDto>.Success(dto);
    }
}
