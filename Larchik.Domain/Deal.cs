namespace Larchik.Domain;

public class Deal
{
    public Guid Id { get; set; }
    public int Quantity { get; set; }
    public decimal Price { get; set; }
    public decimal Amount { get; set; }
    public Operation? Operation { get; set; }
    public string OperationId { get; set; } = string.Empty;
    public Stock Stock { get; set; } = null!;
    public string StockId { get; set; } = string.Empty;
    public decimal Commission { get; set; }

    public DateTime CreatedAt{ get; set; }

    public Account Account { get; set; } = null!;
    public Guid AccountId { get; set; }
}