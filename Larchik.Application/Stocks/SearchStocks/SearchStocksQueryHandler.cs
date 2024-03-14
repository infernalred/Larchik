using Larchik.Application.Dtos;
using Larchik.Application.Helpers;
using Larchik.Persistence.Context;
using Mapster;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Larchik.Application.Stocks.SearchStocks;

public class SearchStocksQueryHandler(DataContext context) 
    : IRequestHandler<SearchStocksQuery, Result<StockDto[]>>
{
    public async Task<Result<StockDto[]>> Handle(SearchStocksQuery request, CancellationToken cancellationToken)
    {
        var result = await context.Stocks
            .Where(x => x.Ticker.Contains(request.Ticker))
            .Take(20)
            .ProjectToType<StockDto>()
            .ToArrayAsync(cancellationToken);

        return Result<StockDto[]>.Success(result);
    }
}