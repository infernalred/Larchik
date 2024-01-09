using AutoMapper;
using AutoMapper.QueryableExtensions;
using Larchik.Application.Dtos;
using Larchik.Application.Helpers;
using Larchik.Persistence.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Larchik.Application.Stocks.GetStock;

public class GetStockQueryHandler(DataContext context, IMapper mapper) 
    : IRequestHandler<GetStockQuery, Result<StockDto?>>
{
    public async Task<Result<StockDto?>> Handle(GetStockQuery request, CancellationToken cancellationToken)
    {
        var stock = await context.Stock
            .ProjectTo<StockDto>(mapper.ConfigurationProvider)
            .FirstOrDefaultAsync(x => x.Ticker == request.Ticker, cancellationToken);

        return Result<StockDto?>.Success(stock);
    }
}