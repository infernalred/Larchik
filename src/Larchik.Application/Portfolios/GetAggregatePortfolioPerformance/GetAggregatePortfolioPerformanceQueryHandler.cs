using Larchik.Application.Contracts;
using Larchik.Application.Helpers;
using Larchik.Application.Models;
using Larchik.Application.Portfolios.Valuation;
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
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .ToListAsync(cancellationToken);

        if (portfolios.Count == 0)
        {
            return Result<IReadOnlyCollection<PortfolioPerformanceDto>>.Success([]);
        }

        var baseCurrency = ResolveBaseCurrency(request.Currency, portfolios);
        if (baseCurrency is null)
        {
            return Result<IReadOnlyCollection<PortfolioPerformanceDto>>.Failure(
                "Portfolios use different reporting currencies. Specify the 'currency' query parameter.");
        }

        var portfolioIds = portfolios.Select(x => x.Id).ToArray();
        var operations = await context.Operations
            .AsNoTracking()
            .Where(x => portfolioIds.Contains(x.PortfolioId))
            .OrderBy(x => x.PortfolioId)
            .ThenBy(x => x.TradeDate)
            .ThenBy(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

        if (operations.Count == 0)
        {
            return Result<IReadOnlyCollection<PortfolioPerformanceDto>>.Success([]);
        }

        var instrumentIds = operations
            .Where(x => x.InstrumentId != null)
            .Select(x => x.InstrumentId!.Value)
            .Distinct()
            .ToArray();

        var instruments = await context.Instruments
            .AsNoTracking()
            .Where(x => instrumentIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, cancellationToken);

        var prices = await context.Prices
            .AsNoTracking()
            .Where(x => instrumentIds.Contains(x.InstrumentId))
            .ToListAsync(cancellationToken);

        var neededCurrencies = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { baseCurrency };
        foreach (var op in operations)
        {
            neededCurrencies.Add(op.CurrencyId);
        }

        foreach (var instrument in instruments.Values)
        {
            neededCurrencies.Add(instrument.CurrencyId);
        }

        var fxRates = await context.FxRates
            .AsNoTracking()
            .Where(x => neededCurrencies.Contains(x.BaseCurrencyId) && neededCurrencies.Contains(x.QuoteCurrencyId))
            .ToListAsync(cancellationToken);

        var data = new HistoricalDataLookup(prices, fxRates);
        var method = request.Method ?? "adjustingAvg";
        var calculator = new PortfolioAnalyticsCalculator();
        var operationsByPortfolio = operations
            .GroupBy(x => x.PortfolioId)
            .ToDictionary(x => x.Key, x => (IReadOnlyList<Operation>)x.ToList());

        var series = portfolios
            .SelectMany(portfolio => calculator.CalculatePerformance(
                portfolio,
                operationsByPortfolio.GetValueOrDefault(portfolio.Id) ?? [],
                instruments,
                data,
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

    private static string? ResolveBaseCurrency(string? requestedCurrency, IReadOnlyCollection<Portfolio> portfolios)
    {
        if (!string.IsNullOrWhiteSpace(requestedCurrency))
        {
            return requestedCurrency.Trim().ToUpperInvariant();
        }

        var distinct = portfolios
            .Select(x => x.ReportingCurrencyId.ToUpperInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return distinct.Length == 1 ? distinct[0] : null;
    }
}
