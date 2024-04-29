using Larchik.Application.Helpers;
using Larchik.Application.Models;
using MediatR;

namespace Larchik.Application.Stocks.CreateStock;

public class CreateStockCommand(StockModel model) : IRequest<Result<Unit>>
{
    public StockModel Model { get; } = model;
}