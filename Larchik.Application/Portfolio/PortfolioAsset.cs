namespace Larchik.Application.Portfolio;

public class PortfolioAsset
{
    public string Ticker { get; set; } = null!;
    public string CompanyName { get; set; } = null!;
    public string Sector { get; set; } = null!;
    public string Type { get; set; } = null!;
    public decimal Quantity { get; set; }
    public decimal Price { get; set; }
    public decimal AmountMarket { get; set; }
    public decimal AmountMarketCurrency { get; set; }
    public decimal AveragePrice { get; set; }
    public decimal AmountAverage { get; set; }
    public decimal AmountAverageCurrency { get; set; }
    public decimal ProfitCurrency => AmountMarketCurrency - AmountAverageCurrency;
}