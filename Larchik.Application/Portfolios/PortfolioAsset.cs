namespace Larchik.Application.Portfolios;

public class PortfolioAsset
{
    public string Ticker { get; set; } = null!;
    public string CompanyName { get; set; } = null!;
    public string Sector { get; set; } = null!;
    public string Type { get; set; } = null!;
    public decimal Quantity { get; set; }
    public decimal Price { get; set; }
    public decimal AveragePrice { get; set; }
}