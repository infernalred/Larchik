using Larchik.Application.Contracts;
using Larchik.Application.Helpers;
using Larchik.Application.Models;
using Larchik.Persistence.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Larchik.Application.Portfolios.ClearPortfolioData;

public class ClearPortfolioDataCommandHandler(LarchikContext context, IUserAccessor userAccessor)
    : IRequestHandler<ClearPortfolioDataCommand, Result<ClearPortfolioDataResultDto>>
{
    public async Task<Result<ClearPortfolioDataResultDto>> Handle(
        ClearPortfolioDataCommand request,
        CancellationToken cancellationToken)
    {
        var userId = userAccessor.GetUserId();
        var portfolioExists = await context.Portfolios
            .AnyAsync(x => x.Id == request.Id && x.UserId == userId, cancellationToken);

        if (!portfolioExists)
        {
            return Result<ClearPortfolioDataResultDto>.Failure("Not found");
        }

        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

        var deletedPositionSnapshots = await context.PositionSnapshots
            .Where(x => x.PortfolioId == request.Id)
            .ExecuteDeleteAsync(cancellationToken);

        var deletedPortfolioSnapshots = await context.PortfolioSnapshots
            .Where(x => x.PortfolioId == request.Id)
            .ExecuteDeleteAsync(cancellationToken);

        var deletedLots = await context.Lots
            .Where(x => x.PortfolioId == request.Id)
            .ExecuteDeleteAsync(cancellationToken);

        var deletedCashBalances = await context.CashBalances
            .Where(x => x.PortfolioId == request.Id)
            .ExecuteDeleteAsync(cancellationToken);

        var deletedOperations = await context.Operations
            .Where(x => x.PortfolioId == request.Id)
            .ExecuteDeleteAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return Result<ClearPortfolioDataResultDto>.Success(new ClearPortfolioDataResultDto(
            deletedOperations,
            deletedPositionSnapshots,
            deletedPortfolioSnapshots,
            deletedLots,
            deletedCashBalances));
    }
}
