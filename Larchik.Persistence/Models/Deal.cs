using Larchik.Persistence.Models;

namespace Larchik.Domain;

public class Deal
{
    public Guid Id { get; set; }
    public int Quantity { get; set; }
    public decimal Price { get; set; }
    public decimal Amount { get; set; }
    public DealType? Type { get; set; }
    public int TypeId { get; set; }
    public string OperationId { get; set; } = string.Empty;
    public Currency Currency { get; set; } = null!;
    public string CurrencyId { get; set; } = null!;
    public Stock? Stock { get; set; }
    public string? StockId { get; set; }
    public decimal Commission { get; set; }
    public DateTime CreatedAt{ get; set; }
    public Account Account { get; set; } = null!;
    public Guid AccountId { get; set; }
    public AppUser User { get; set; } = null!;
    public string UserId { get; set; } = null!;
}