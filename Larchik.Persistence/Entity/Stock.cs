using Larchik.Persistence.Enum;

namespace Larchik.Persistence.Entity;

public class Stock
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public string Ticker { get; set; } = null!;
    public string Isin { get; set; } = null!;
    public StockKind Kind { get; set; }
    public string CurrencyId { get; set; } = null!;
    public string SectorId { get; set; } = null!;
    public double LastPrice { get; set; }
    public DateTime CreatedAt { get; set; }
    public Guid CreatedBy { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Guid UpdatedBy { get; set; }

    public Currency? Currency { get; set; }
    public Sector? Sector { get; set; }
}