namespace Larchik.Domain;

public class Deal
{
    public Guid Id { get; set; }
    public int Quantity { get; set; }
    public double Price { get; set; }
    public double Amount { get; set; }
    public Operation? Operation { get; set; }
    public string OperationId { get; set; } = string.Empty;
    public Stock Stock { get; set; } = null!;
    public string StockId { get; set; } = string.Empty;
    public double Commission { get; set; }
    public DateTime CreatedAt{ get; set; }
    public Account Account { get; set; } = null!;
    public Guid AccountId { get; set; }
}