using Larchik.Application.Contracts;
using Larchik.Application.Helpers;
using Larchik.Application.Models;
using Larchik.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace Larchik.Application.Portfolios.GetPortfolios;

public class GetPortfoliosQueryHandler(LarchikContext context, IUserAccessor userAccessor)
{
    public async Task<Result<IReadOnlyCollection<PortfolioDto>>> Handle(GetPortfoliosQuery request, CancellationToken cancellationToken)
    {
        var userId = userAccessor.GetUserId();
        var items = await context.Portfolios
            .Where(x => x.UserId == userId)
            .ProjectToDto()
            .ToListAsync(cancellationToken);

        return Result<IReadOnlyCollection<PortfolioDto>>.Success(items);
    }
}
