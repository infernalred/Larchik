namespace Larchik.Application.Models.Market;

public class StockPrice
{
    public string Figi { get; set; } = null!;
    public double LastPrice { get; set; }
}