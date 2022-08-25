using Larchik.Application.Dtos;
using Larchik.Domain.Enum;

namespace Larchik.Application.Portfolios;

public class PortfolioAsset
{
    public StockDto Stock { get; }
    private decimal _rate;
    public decimal Quantity { get; init; }
    public decimal AmountMarket => Stock.Type == StockKind.Money 
            ? Quantity 
            : Quantity * (decimal)Stock.LastPrice;

    public decimal AmountMarketCurrency => Math.Round(AmountMarket * _rate, 2);
    public decimal AveragePrice { get; set; }
    public decimal AmountAverage => Stock.Type == StockKind.Money 
        ? Quantity 
        : Quantity * AveragePrice;
    public decimal Profit => AmountMarket - AmountAverage;

    public PortfolioAsset(StockDto stock, decimal rate, decimal quantity, decimal averagePrice)
    {
        Stock = stock;
        _rate = rate;
        Quantity = quantity;
        AveragePrice = averagePrice;
    }
}