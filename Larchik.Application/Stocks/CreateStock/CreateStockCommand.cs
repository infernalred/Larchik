using Larchik.Application.Dtos;
using Larchik.Application.Helpers;
using MediatR;

namespace Larchik.Application.Stocks.CreateStock;

public class CreateStockCommand(StockDto model) : IRequest<Result<Unit>>
{
    public StockDto Stock { get; set; } = model;
}