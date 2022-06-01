namespace Larchik.Application.Portfolios;

public class PortfolioAsset
{
    public string Ticker { get; set; } = null!;
    public string CompanyName { get; set; } = null!;
    public string Sector { get; set; } = null!;
    public string Type { get; set; } = null!;
    public double Quantity { get; set; }
    public double Price { get; set; }
    public double AveragePrice { get; set; }
}