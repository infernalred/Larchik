using Larchik.Application.Contracts;
using Larchik.Application.Helpers;
using Larchik.Application.Models;
using Larchik.Application.Portfolios;
using Larchik.Application.Portfolios.Valuation;
using Larchik.Persistence.Context;
using Larchik.Persistence.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Larchik.Application.Portfolios.GetPortfoliosSummary;

public class GetPortfoliosSummaryQueryHandler(LarchikContext context, IUserAccessor userAccessor)
    : IRequestHandler<GetPortfoliosSummaryQuery, Result<PortfoliosSummaryDto>>
{
    private const string DefaultValuationMethod = "adjustingAvg";

    public async Task<Result<PortfoliosSummaryDto>> Handle(
        GetPortfoliosSummaryQuery request,
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
            return Result<PortfoliosSummaryDto>.Failure("No portfolios found");
        }

        var baseCurrency = ResolveBaseCurrency(request.Currency, portfolios);
        if (baseCurrency is null)
        {
            return Result<PortfoliosSummaryDto>.Failure(
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
            .AsNoTracking()
            .Where(x => instrumentIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, cancellationToken);
        var corporateActions = await InstrumentCorporateActionOperationMerger.LoadAsync(context, instrumentIds, cancellationToken);

        var prices = await context.Prices
            .AsNoTracking()
            .Where(x => instrumentIds.Contains(x.InstrumentId) && x.Date <= asOfDateTime)
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
        var operationsByPortfolio = operations
            .GroupBy(x => x.PortfolioId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<Operation>)g.ToList());

        var method = request.Method ?? DefaultValuationMethod;
        var calculator = new PortfolioAnalyticsCalculator();

        decimal totalNetInflowBase = 0;
        decimal totalGrossDepositsBase = 0;
        decimal totalGrossWithdrawalsBase = 0;
        decimal totalCashBase = 0;
        decimal totalPositionsValueBase = 0;
        decimal totalRealizedBase = 0;
        decimal totalUnrealizedBase = 0;

        foreach (var portfolio in portfolios)
        {
            var portfolioOperations = operationsByPortfolio.GetValueOrDefault(portfolio.Id) ?? [];
            var mergedOperations = InstrumentCorporateActionOperationMerger.Merge(portfolioOperations, corporateActions, instruments);
            var summary = calculator.CalculateSummary(
                portfolio,
                mergedOperations,
                instruments,
                data,
                method,
                baseCurrency,
                asOfDateTime);

            totalNetInflowBase += summary.NetInflowBase;
            totalGrossDepositsBase += summary.GrossDepositsBase;
            totalGrossWithdrawalsBase += summary.GrossWithdrawalsBase;
            totalCashBase += summary.CashBase;
            totalPositionsValueBase += summary.PositionsValueBase;
            totalRealizedBase += summary.RealizedBase;
            totalUnrealizedBase += summary.UnrealizedBase;
        }

        var navBase = totalCashBase + totalPositionsValueBase;
        var pnlBase = totalRealizedBase + totalUnrealizedBase;

        return Result<PortfoliosSummaryDto>.Success(new PortfoliosSummaryDto
        {
            ReportingCurrencyId = baseCurrency,
            PortfolioCount = portfolios.Count,
            NetInflowBase = totalNetInflowBase,
            GrossDepositsBase = totalGrossDepositsBase,
            GrossWithdrawalsBase = totalGrossWithdrawalsBase,
            CashBase = totalCashBase,
            PositionsValueBase = totalPositionsValueBase,
            RealizedBase = totalRealizedBase,
            UnrealizedBase = totalUnrealizedBase,
            PnlBase = pnlBase,
            ValuationMethod = method,
            NavBase = navBase
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
