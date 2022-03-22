namespace Larchik.Domain;

public class Stock
{
    public string Ticker { get; set; }
    public string CompanyName { get; set; }
    public StockType Type { get; set; }
    public string TypeId { get; set; }
    public Currency Currency { get; set; }
    public string CurrencyId { get; set; }
    public Sector Sector { get; set; }
    public string SectorId { get; set; }
}