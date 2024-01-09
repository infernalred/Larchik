using Larchik.Application.Dtos;
using Larchik.Application.Helpers;
using MediatR;

namespace Larchik.Application.Stocks.GetPagedStocks;

public class GetPagedQuery(StockFilter filter) : IRequest<Result<PagedList<StockDto>>>
{
    public StockFilter Filter { get; } = filter;
}