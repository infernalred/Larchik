using Larchik.Application.Contracts;
using Larchik.Application.Helpers;
using Larchik.Application.Models;
using Larchik.Persistence.Context;
using Mapster;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Larchik.Application.Portfolios.GetPortfolios;

public class GetPortfoliosQueryHandler(LarchikContext context, IUserAccessor userAccessor)
    : IRequestHandler<GetPortfoliosQuery, Result<IReadOnlyCollection<PortfolioDto>>>
{
    public async Task<Result<IReadOnlyCollection<PortfolioDto>>> Handle(GetPortfoliosQuery request, CancellationToken cancellationToken)
    {
        var userId = userAccessor.GetUserId();
        var items = await context.Portfolios
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .ProjectToType<PortfolioDto>()
            .ToListAsync(cancellationToken);

        return Result<IReadOnlyCollection<PortfolioDto>>.Success(items);
    }
}
