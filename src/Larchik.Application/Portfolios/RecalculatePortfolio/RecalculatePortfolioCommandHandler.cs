using Larchik.Application.Contracts;
using Larchik.Application.Helpers;
using Larchik.Application.Models;
using Larchik.Persistence.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Larchik.Application.Portfolios.RecalculatePortfolio;

public class RecalculatePortfolioCommandHandler(
    LarchikContext context,
    IUserAccessor userAccessor,
    IPortfolioRecalcService recalc)
    : IRequestHandler<RecalculatePortfolioCommand, Result<RecalculatePortfolioResultDto>>
{
    public async Task<Result<RecalculatePortfolioResultDto>> Handle(
        RecalculatePortfolioCommand request,
        CancellationToken cancellationToken)
    {
        var userId = userAccessor.GetUserId();
        var portfolioExists = await context.Portfolios
            .AsNoTracking()
            .AnyAsync(x => x.Id == request.Id && x.UserId == userId, cancellationToken);

        if (!portfolioExists)
        {
            return Result<RecalculatePortfolioResultDto>.Failure("Not found");
        }

        var operationDates = await context.Operations
            .AsNoTracking()
            .Where(x => x.PortfolioId == request.Id)
            .OrderBy(x => x.TradeDate)
            .Select(x => x.TradeDate)
            .ToListAsync(cancellationToken);

        if (operationDates.Count == 0)
        {
            return Result<RecalculatePortfolioResultDto>.Success(new RecalculatePortfolioResultDto(
                DateTime.UtcNow.Date,
                0));
        }

        var recalculatedFrom = operationDates[0];
        await recalc.ScheduleRebuild(request.Id, recalculatedFrom, cancellationToken);

        return Result<RecalculatePortfolioResultDto>.Success(new RecalculatePortfolioResultDto(
            recalculatedFrom,
            operationDates.Count));
    }
}
