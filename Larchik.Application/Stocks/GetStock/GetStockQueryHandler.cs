using Larchik.Application.Helpers;
using Larchik.Application.Models;
using Larchik.Persistence.Context;
using Mapster;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Larchik.Application.Stocks.GetStock;

public class GetStockQueryHandler(LarchikContext context) : IRequestHandler<GetStockQuery, Result<StockDto?>>
{
    public async Task<Result<StockDto?>> Handle(GetStockQuery request, CancellationToken cancellationToken)
    {
        var stock = await context.Stocks
            .ProjectToType<StockDto>()
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);

        return Result<StockDto?>.Success(stock);
    }
}