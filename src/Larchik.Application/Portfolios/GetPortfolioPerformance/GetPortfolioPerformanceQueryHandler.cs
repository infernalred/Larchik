using Larchik.Application.Contracts;
using Larchik.Application.Helpers;
using Larchik.Application.Models;
using Larchik.Persistence.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Larchik.Application.Portfolios.GetPortfolioPerformance;

public class GetPortfolioPerformanceQueryHandler(LarchikContext context, IUserAccessor userAccessor)
    : IRequestHandler<GetPortfolioPerformanceQuery, Result<IReadOnlyCollection<PortfolioPerformanceDto>>?>
{
    private const string DefaultValuationMethod = "adjustingAvg";

    public async Task<Result<IReadOnlyCollection<PortfolioPerformanceDto>>?> Handle(
        GetPortfolioPerformanceQuery request,
        CancellationToken cancellationToken)
    {
        var userId = userAccessor.GetUserId();
        var portfolio = await context.Portfolios
            .Include(x => x.Broker)
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == request.Id && x.UserId == userId, cancellationToken);

        if (portfolio is null)
        {
            return null;
        }

        var operations = await context.Operations
            .AsNoTracking()
            .Where(x => x.PortfolioId == request.Id)
            .OrderBy(x => x.TradeDate)
            .ThenBy(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

        if (operations.Count == 0)
        {
            return Result<IReadOnlyCollection<PortfolioPerformanceDto>>.Success([]);
        }

        var maxPriceDate = (request.To?.Date ?? DateTime.UtcNow.Date).AddDays(1).AddTicks(-1);
        var analytics = await PortfolioAnalyticsQueryHelper.LoadAsync(
            context,
            operations,
            portfolio.ReportingCurrencyId,
            maxPriceDate,
            cancellationToken);

        var method = request.Method ?? DefaultValuationMethod;
        var performance = new PortfolioAnalyticsCalculator().CalculatePerformance(
            portfolio,
            analytics.Operations,
            analytics.Instruments,
            analytics.Data,
            method,
            portfolio.ReportingCurrencyId,
            request.From,
            request.To);

        return Result<IReadOnlyCollection<PortfolioPerformanceDto>>.Success(performance);
    }
}
