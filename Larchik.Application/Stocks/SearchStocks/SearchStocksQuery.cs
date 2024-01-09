using Larchik.Application.Dtos;
using Larchik.Application.Helpers;
using MediatR;

namespace Larchik.Application.Stocks.SearchStocks;

public class SearchStocksQuery(string ticker) : IRequest<Result<StockDto[]>>
{
    public string Ticker { get; } = ticker;
}