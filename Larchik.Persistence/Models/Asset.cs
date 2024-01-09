using Larchik.Persistence.Models;

namespace Larchik.Domain;

public class Asset
{
    public Guid Id { get; set; }
    public Account Account { get; set; } = null!;
    public Guid AccountId { get; set; }
    public Stock Stock { get; set; } = null!;
    public string StockId { get; set; } = null!;
    public decimal Quantity { get; set; }
}