using Larchik.Application.Contracts;
using Larchik.Application.Helpers;
using Larchik.Application.Models;
using Larchik.Persistence.Context;
using Mapster;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Larchik.Application.Operations.GetOperation;

public class GetOperationQueryHandler(LarchikContext context, IUserAccessor userAccessor)
    : IRequestHandler<GetOperationQuery, Result<OperationDto?>>
{
    public async Task<Result<OperationDto?>> Handle(GetOperationQuery request, CancellationToken cancellationToken)
    {
        var userId = userAccessor.GetUserId();
        var op = await context.Operations
            .AsNoTracking()
            .Where(x => x.Id == request.Id && x.Portfolio != null && x.Portfolio.UserId == userId)
            .ProjectToType<OperationDto>()
            .FirstOrDefaultAsync(cancellationToken);

        return Result<OperationDto?>.Success(op);
    }
}
