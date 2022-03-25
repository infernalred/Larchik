namespace Larchik.Application.Portfolios;

public class PortfolioAsset
{
    public string Ticker { get; set; }
    public string CompanyName { get; set; }
    public string Sector { get; set; }
    public string Category { get; set; }
    public decimal Qantity { get; set; }
    public decimal Price { get; set; }
    public decimal AveragePrice { get; set; }
}