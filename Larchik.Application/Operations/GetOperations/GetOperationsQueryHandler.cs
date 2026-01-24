using Larchik.Application.Contracts;
using Larchik.Application.Helpers;
using Larchik.Application.Models;
using Larchik.Persistence.Context;
using Mapster;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Larchik.Application.Operations.GetOperations;

public class GetOperationsQueryHandler(LarchikContext context, IUserAccessor userAccessor)
    : IRequestHandler<GetOperationsQuery, Result<IReadOnlyCollection<OperationDto>>>
{
    public async Task<Result<IReadOnlyCollection<OperationDto>>> Handle(GetOperationsQuery request, CancellationToken cancellationToken)
    {
        var userId = userAccessor.GetUserId();
        var ops = await context.Operations
            .AsNoTracking()
            .Where(x => x.PortfolioId == request.PortfolioId && x.Portfolio != null && x.Portfolio.UserId == userId)
            .OrderBy(x => x.TradeDate)
            .ProjectToType<OperationDto>()
            .ToListAsync(cancellationToken);

        return Result<IReadOnlyCollection<OperationDto>>.Success(ops);
    }
}
