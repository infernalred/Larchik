using Larchik.Application.Helpers;
using Larchik.Application.Models;
using MediatR;

namespace Larchik.Application.Stocks.EditStock;

public record EditStockCommand(Guid Id, StockModel Model) : IRequest<Result<Unit>>;