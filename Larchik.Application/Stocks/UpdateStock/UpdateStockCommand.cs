using Larchik.Application.Dtos;
using Larchik.Application.Helpers;
using MediatR;

namespace Larchik.Application.Stocks.UpdateStock;

public class UpdateStockCommand(Guid id, StockDto model) : IRequest<Result<Unit>?>
{
    public Guid Id { get; } = id;
    public StockDto Stock { get; } = model;
}