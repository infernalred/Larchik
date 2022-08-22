namespace Larchik.Application.Dtos;

public class StockDto
{
    public string Ticker { get; set; } = null!;
    public string Figi { get; set; } = null!;
    public string CompanyName { get; set; } = null!;
    public string Type { get; set; } = null!;
    public string Currency { get; set; } = null!;
    public string Sector { get; set; } = null!;
    public double LastPrice { get; set; }
}