using Larchik.Domain.Enum;

namespace Larchik.Domain;

public class Stock
{
    public string Ticker { get; set; } = string.Empty;
    public string Figi { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public StockKind Type { get; set; }
    public string TypeId { get; set; } = string.Empty;
    public Currency? Currency { get; set; }
    public string CurrencyId { get; set; } = string.Empty;
    public Sector? Sector { get; set; }
    public string SectorId { get; set; } = string.Empty;
    public double LastPrice { get; set; }
}