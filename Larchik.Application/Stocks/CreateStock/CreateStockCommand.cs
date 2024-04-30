using Larchik.Application.Helpers;
using Larchik.Application.Models;
using MediatR;

namespace Larchik.Application.Stocks.CreateStock;

public record CreateStockCommand(StockModel Model) : IRequest<Result<Unit>>;