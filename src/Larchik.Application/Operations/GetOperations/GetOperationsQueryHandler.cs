using Larchik.Application.Contracts;
using Larchik.Application.Common.Paging;
using Larchik.Application.Helpers;
using Larchik.Application.Models;
using Larchik.Persistence.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Larchik.Application.Operations.GetOperations;

public class GetOperationsQueryHandler(LarchikContext context, IUserAccessor userAccessor)
    : IRequestHandler<GetOperationsQuery, Result<PagedResult<OperationDto>>>
{
    private const int MaxPageSize = 200;

    public async Task<Result<PagedResult<OperationDto>>> Handle(GetOperationsQuery request, CancellationToken cancellationToken)
    {
        var userId = userAccessor.GetUserId();
        var result = await context.Operations
            .Where(x =>
                x.PortfolioId == request.PortfolioId &&
                x.Portfolio != null &&
                x.Portfolio.UserId == userId)
            .WhereVisibleInPortfolio()
            .OrderByDescending(x => x.TradeDate)
            .ThenByDescending(x => x.CreatedAt)
            .ProjectToDto()
            .ToPagedResultAsync(request.Paging, MaxPageSize, cancellationToken);

        return Result<PagedResult<OperationDto>>.Success(result);
    }
}
