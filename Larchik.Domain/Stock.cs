namespace Larchik.Domain;

public class Stock
{
    public string Tiker { get; set; }
    public StockType Type { get; set; }
    public Currency Currency { get; set; }
    public Sector Sector { get; set; }
}