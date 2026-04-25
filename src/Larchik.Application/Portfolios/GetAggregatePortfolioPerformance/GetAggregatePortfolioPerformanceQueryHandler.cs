using Larchik.Application.Contracts;
using Larchik.Application.Helpers;
using Larchik.Application.Models;
using Larchik.Persistence.Context;
using Larchik.Persistence.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Larchik.Application.Portfolios.GetAggregatePortfolioPerformance;

public class GetAggregatePortfolioPerformanceQueryHandler(LarchikContext context, IUserAccessor userAccessor)
    : IRequestHandler<GetAggregatePortfolioPerformanceQuery, Result<IReadOnlyCollection<PortfolioPerformanceDto>>>
{
    public async Task<Result<IReadOnlyCollection<PortfolioPerformanceDto>>> Handle(
        GetAggregatePortfolioPerformanceQuery request,
        CancellationToken cancellationToken)
    {
        var userId = userAccessor.GetUserId();
        var portfolios = await context.Portfolios
            .Include(x => x.Broker)
            .Where(x => x.UserId == userId)
            .ToListAsync(cancellationToken);

        if (portfolios.Count == 0)
        {
            return Result<IReadOnlyCollection<PortfolioPerformanceDto>>.Success([]);
        }

        var baseCurrency = PortfolioAnalyticsQueryHelper.ResolveBaseCurrency(request.Currency, portfolios);
        if (baseCurrency is null)
        {
            return Result<IReadOnlyCollection<PortfolioPerformanceDto>>.Failure(
                "Portfolios use different reporting currencies. Specify the 'currency' query parameter.");
        }

        var portfolioIds = portfolios.Select(x => x.Id).ToArray();
        var operations = await context.Operations
            .Where(x => portfolioIds.Contains(x.PortfolioId))
            .OrderBy(x => x.PortfolioId)
            .ThenBy(x => x.TradeDate)
            .ThenBy(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

        if (operations.Count == 0)
        {
            return Result<IReadOnlyCollection<PortfolioPerformanceDto>>.Success([]);
        }

        var method = request.Method ?? "adjustingAvg";
        var calculator = new PortfolioAnalyticsCalculator();
        var maxPriceDate = PortfolioAnalyticsQueryHelper.NormalizeMaxPriceDateUtc(request.To);
        var analytics = await PortfolioAnalyticsQueryHelper.LoadAsync(
            context,
            operations,
            baseCurrency,
            maxPriceDate,
            cancellationToken);
        var operationsByPortfolio = analytics.Operations
            .GroupBy(x => x.PortfolioId)
            .ToDictionary(x => x.Key, x => (IReadOnlyList<Operation>)x.ToList());

        var series = portfolios
            .SelectMany(portfolio => calculator.CalculatePerformance(
                portfolio,
                operationsByPortfolio.GetValueOrDefault(portfolio.Id) ?? [],
                analytics.Instruments,
                analytics.Data,
                method,
                baseCurrency,
                request.From,
                request.To))
            .GroupBy(x => x.Period)
            .OrderBy(x => x.Key)
            .Select(group =>
            {
                var startNav = group.Sum(x => x.StartNavBase);
                var pnl = group.Sum(x => x.PnlBase);
                return new PortfolioPerformanceDto
                {
                    Period = group.Key,
                    StartDate = group.Min(x => x.StartDate),
                    EndDate = group.Max(x => x.EndDate),
                    ReportingCurrencyId = baseCurrency,
                    ValuationMethod = method,
                    StartNavBase = startNav,
                    EndNavBase = group.Sum(x => x.EndNavBase),
                    NetInflowBase = group.Sum(x => x.NetInflowBase),
                    PnlBase = pnl,
                    ReturnPct = startNav != 0 ? pnl / startNav : 0m,
                    RealizedBase = group.Sum(x => x.RealizedBase),
                    UnrealizedBase = group.Sum(x => x.UnrealizedBase)
                };
            })
            .ToList();

        return Result<IReadOnlyCollection<PortfolioPerformanceDto>>.Success(series);
    }
}
