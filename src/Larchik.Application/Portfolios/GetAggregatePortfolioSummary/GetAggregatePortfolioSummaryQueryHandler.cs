using Larchik.Application.Contracts;
using Larchik.Application.Helpers;
using Larchik.Application.Models;
using Larchik.Application.Portfolios.Valuation;
using Larchik.Persistence.Context;
using Larchik.Persistence.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Larchik.Application.Portfolios.GetAggregatePortfolioSummary;

public class GetAggregatePortfolioSummaryQueryHandler(LarchikContext context, IUserAccessor userAccessor)
    : IRequestHandler<GetAggregatePortfolioSummaryQuery, Result<PortfolioSummaryDto>>
{
    public async Task<Result<PortfolioSummaryDto>> Handle(
        GetAggregatePortfolioSummaryQuery request,
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
            return Result<PortfolioSummaryDto>.Failure("No portfolios found");
        }

        var baseCurrency = ResolveBaseCurrency(request.Currency, portfolios);
        if (baseCurrency is null)
        {
            return Result<PortfolioSummaryDto>.Failure(
                "Portfolios use different reporting currencies. Specify the 'currency' query parameter.");
        }

        var asOfDateTime = DateTime.UtcNow;
        var portfolioIds = portfolios.Select(x => x.Id).ToArray();
        var operations = await context.Operations
            .AsNoTracking()
            .Where(x => portfolioIds.Contains(x.PortfolioId) && x.TradeDate <= asOfDateTime)
            .OrderBy(x => x.PortfolioId)
            .ThenBy(x => x.TradeDate)
            .ThenBy(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

        var instrumentIds = operations
            .Where(x => x.InstrumentId != null)
            .Select(x => x.InstrumentId!.Value)
            .Distinct()
            .ToArray();

        var instruments = await context.Instruments
            .Include(x => x.Category)
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

        var fxRates = await MarketFxRateLoader.LoadAsync(context, neededCurrencies, cancellationToken);

        var data = new HistoricalDataLookup(prices, fxRates);
        var method = request.Method ?? "adjustingAvg";
        var calculator = new PortfolioAnalyticsCalculator();
        var operationsByPortfolio = operations
            .GroupBy(x => x.PortfolioId)
            .ToDictionary(x => x.Key, x => (IReadOnlyList<Operation>)x.ToList());

        var summaries = portfolios
            .Select(portfolio => calculator.CalculateSummary(
                portfolio,
                operationsByPortfolio.GetValueOrDefault(portfolio.Id) ?? [],
                instruments,
                data,
                method,
                baseCurrency,
                asOfDateTime))
            .ToList();

        var cash = summaries
            .SelectMany(x => x.Cash)
            .GroupBy(x => x.CurrencyId, StringComparer.OrdinalIgnoreCase)
            .Select(x => new CashBalanceDto
            {
                CurrencyId = x.Key.ToUpperInvariant(),
                Amount = x.Sum(y => y.Amount),
                AmountInBase = x.Sum(y => y.AmountInBase)
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
                    AverageCost = weightedCost
                };
            })
            .OrderByDescending(x => x.MarketValueBase)
            .ToList();

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

        return Result<PortfolioSummaryDto>.Success(new PortfolioSummaryDto
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
            NavBase = summaries.Sum(x => x.NavBase),
            ValuationMethod = method,
            Cash = cash,
            Positions = positions,
            RealizedByInstrument = realized
        });
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
