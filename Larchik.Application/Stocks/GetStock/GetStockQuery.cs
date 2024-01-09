using Larchik.Application.Dtos;
using Larchik.Application.Helpers;
using MediatR;

namespace Larchik.Application.Stocks.GetStock;

public class GetStockQuery(string ticker) : IRequest<Result<StockDto?>>
{
    public string Ticker { get; } = ticker;
}