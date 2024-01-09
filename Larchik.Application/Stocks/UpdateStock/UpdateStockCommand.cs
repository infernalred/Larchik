using Larchik.Application.Dtos;
using Larchik.Application.Helpers;
using MediatR;

namespace Larchik.Application.Stocks.UpdateStock;

public class UpdateStockCommand : IRequest<Result<Unit>?>
{
    public StockDto Stock { get; set; } = null!;
}