using Larchik.Domain;
using Larchik.Domain.Enum;

namespace Larchik.Persistence.Models;

public class Stock
{
    public string Ticker { get; set; } = null!;
    public string Figi { get; set; } = null!;
    public string CompanyName { get; set; } = null!;
    public StockKind Type { get; set; }
    public string TypeId { get; set; } = null!;
    public Currency? Currency { get; set; }
    public string CurrencyId { get; set; } = null!;
    public Sector? Sector { get; set; }
    public string SectorId { get; set; } = null!;
    public double LastPrice { get; set; }
    public DateTime CreatedAt { get; set; }
    public Guid CreatedBy { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Guid UpdatedBy { get; set; }
}