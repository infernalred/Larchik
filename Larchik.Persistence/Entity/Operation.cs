using Larchik.Persistence.Enum;

namespace Larchik.Persistence.Entity;

public class Operation
{
    public Guid Id { get; set; }
    public int Quantity { get; set; }
    public decimal Price { get; set; }
    public decimal Commission { get; set; }
    public decimal Amount { get; set; }
    public Guid StockId { get; set; }
    public OperationKind Kind { get; set; }
    public string CurrencyId { get; set; } = null!;
    public DateTime CreatedAt{ get; set; }
    public Guid AccountId { get; set; }
    public Guid UserId { get; set; }

    public Currency? Currency { get; set; }
    public Account? Account { get; set; }
    public Stock? Stock { get; set; }
    public AppUser? User { get; set; }
}