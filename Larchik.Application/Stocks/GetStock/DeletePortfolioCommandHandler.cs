using Larchik.Application.Contracts;
using Larchik.Application.Helpers;
using Larchik.Persistence.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Larchik.Application.Stocks.GetStock;

public class DeletePortfolioCommandHandler(LarchikContext context, IUserAccessor userAccessor)
    : IRequestHandler<DeletePortfolioCommand, Result<Unit>>
{
    public async Task<Result<Unit>> Handle(DeletePortfolioCommand request, CancellationToken cancellationToken)
    {
        var userId = userAccessor.GetUserId();
        var portfolio = await context.Portfolios
            .FirstOrDefaultAsync(x => x.Id == request.Id && x.UserId == userId, cancellationToken);

        if (portfolio is null) return Result<Unit>.Failure("Not found");

        context.Portfolios.Remove(portfolio);
        await context.SaveChangesAsync(cancellationToken);

        return Result<Unit>.Success(Unit.Value);
    }
}
