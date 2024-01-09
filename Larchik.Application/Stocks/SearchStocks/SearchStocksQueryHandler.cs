using AutoMapper;
using AutoMapper.QueryableExtensions;
using Larchik.Application.Dtos;
using Larchik.Application.Helpers;
using Larchik.Persistence.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Larchik.Application.Stocks.SearchStocks;

public class SearchStocksQueryHandler(DataContext context, IMapper mapper) 
    : IRequestHandler<SearchStocksQuery, Result<StockDto[]>>
{
    public async Task<Result<StockDto[]>> Handle(SearchStocksQuery request, CancellationToken cancellationToken)
    {
        var result = await context.Stock
            .Where(x => x.Ticker.Contains(request.Ticker))
            .Take(20)
            .ProjectTo<StockDto>(mapper.ConfigurationProvider)
            .ToArrayAsync(cancellationToken);

        return Result<StockDto[]>.Success(result);
    }
}