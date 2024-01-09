using Larchik.Application.Dtos;
using Larchik.Application.Helpers;
using MediatR;

namespace Larchik.Application.Stocks.CreateStock;

public class CreateStockCommand : IRequest<Result<Unit>>
{
    public StockDto Stock { get; set; } = null!;
}