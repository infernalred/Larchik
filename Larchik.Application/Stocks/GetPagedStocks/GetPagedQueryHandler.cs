using Larchik.Application.Dtos;
using Larchik.Application.Helpers;
using Larchik.Persistence.Context;
using Mapster;
using MediatR;

namespace Larchik.Application.Stocks.GetPagedStocks;

public class GetPagedQueryHandler(DataContext context) 
    : IRequestHandler<GetPagedQuery, Result<PagedList<StockDto>>>
{
    public async Task<Result<PagedList<StockDto>>> Handle(GetPagedQuery request, CancellationToken cancellationToken)
    {
        var query = context.Stocks
            .OrderBy(x => x.Kind)
            .ProjectToType<StockDto>()
            .AsQueryable();

        return Result<PagedList<StockDto>>.Success(
            await PagedList<StockDto>.CreateAsync(query, request.Filter.PageNumber, request.Filter.PageSize));
    }
}