using Larchik.Application.Dtos;
using Larchik.Application.Helpers;
using MediatR;

namespace Larchik.Application.Stocks.GetStock;

public class GetStockQuery(Guid id) : IRequest<Result<StockDto?>>
{
    public Guid Id { get; } = id;
}