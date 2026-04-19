using Larchik.Application.Contracts;
using Larchik.Application.Helpers;
using Larchik.Application.Models;
using Larchik.Persistence.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Larchik.Application.Portfolios.GetPortfolioSummary;

public class GetPortfolioSummaryQueryHandler(LarchikContext context, IUserAccessor userAccessor)
    : IRequestHandler<GetPortfolioSummaryQuery, Result<PortfolioSummaryDto>>
{
    private const string DefaultValuationMethod = "adjustingAvg";

    public async Task<Result<PortfolioSummaryDto>> Handle(GetPortfolioSummaryQuery request, CancellationToken cancellationToken)
    {
        var userId = userAccessor.GetUserId();
        var portfolio = await context.Portfolios
            .Include(x => x.Broker)
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == request.Id && x.UserId == userId, cancellationToken);

        if (portfolio is null) return null!;

        var asOfDateTime = DateTime.UtcNow;
        var operations = await context.Operations
            .AsNoTracking()
            .Where(x => x.PortfolioId == request.Id && x.TradeDate <= asOfDateTime)
            .OrderBy(x => x.TradeDate)
            .ThenBy(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

        var analytics = await PortfolioAnalyticsQueryHelper.LoadAsync(
            context,
            operations,
            portfolio.ReportingCurrencyId,
            asOfDateTime,
            cancellationToken);

        var method = request.Method ?? DefaultValuationMethod;
        var summary = new PortfolioAnalyticsCalculator().CalculateSummary(
            portfolio,
            analytics.Operations,
            analytics.Instruments,
            analytics.Data,
            method,
            portfolio.ReportingCurrencyId,
            asOfDateTime);

        return Result<PortfolioSummaryDto>.Success(summary);
    }
}
