using Larchik.Application.Contracts;
using Larchik.Application.Helpers;
using Larchik.Persistence.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Larchik.Application.Stocks.GetStock;

public class EditPortfolioCommandHandler(LarchikContext context, IUserAccessor userAccessor)
    : IRequestHandler<EditPortfolioCommand, Result<Unit>?>
{
    public async Task<Result<Unit>?> Handle(EditPortfolioCommand request, CancellationToken cancellationToken)
    {
        var userId = userAccessor.GetUserId();
        var portfolio = await context.Portfolios
            .AsTracking()
            .FirstOrDefaultAsync(x => x.Id == request.Id && x.UserId == userId, cancellationToken);

        if (portfolio is null) return null;

        portfolio.Name = request.Model.Name;
        portfolio.BrokerId = request.Model.BrokerId;
        portfolio.ReportingCurrencyId = request.Model.ReportingCurrencyId.ToUpperInvariant();

        await context.SaveChangesAsync(cancellationToken);

        return Result<Unit>.Success(Unit.Value);
    }
}
