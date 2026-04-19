using Larchik.Application.Contracts;
using Larchik.Application.Helpers;
using Larchik.Persistence.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Larchik.Application.Portfolios.EditPortfolio;

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

        var resolvedInputResult = await PortfolioWriteHelper.ResolveInputAsync(context, request.Model, cancellationToken);
        if (!resolvedInputResult.IsSuccess)
        {
            return Result<Unit>.Failure(resolvedInputResult.Error!);
        }

        var resolvedInput = resolvedInputResult.Value!;
        portfolio.Name = resolvedInput.Name;
        portfolio.BrokerId = resolvedInput.BrokerId;
        portfolio.ReportingCurrencyId = resolvedInput.ReportingCurrencyId;

        await context.SaveChangesAsync(cancellationToken);

        return Result<Unit>.Success(Unit.Value);
    }
}
