using Larchik.Application.Helpers;
using Larchik.Application.Models;
using MediatR;

namespace Larchik.Application.Stocks.GetStock;

public record GetStockQuery(Guid Id) : IRequest<Result<StockDto?>>;