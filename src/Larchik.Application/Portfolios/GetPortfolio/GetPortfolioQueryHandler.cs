using Larchik.Application.Contracts;
using Larchik.Application.Helpers;
using Larchik.Application.Models;
using Larchik.Persistence.Context;
using Mapster;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Larchik.Application.Portfolios.GetPortfolio;

public class GetPortfolioQueryHandler(LarchikContext context, IUserAccessor userAccessor)
    : IRequestHandler<GetPortfolioQuery, Result<PortfolioDto?>>
{
    public async Task<Result<PortfolioDto?>> Handle(GetPortfolioQuery request, CancellationToken cancellationToken)
    {
        var userId = userAccessor.GetUserId();
        var item = await context.Portfolios
            .AsNoTracking()
            .Where(x => x.UserId == userId && x.Id == request.Id)
            .ProjectToType<PortfolioDto>()
            .FirstOrDefaultAsync(cancellationToken);

        return Result<PortfolioDto?>.Success(item);
    }
}
