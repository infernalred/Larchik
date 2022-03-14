namespace Larchik.Domain;

public class Asset
{
    public Guid Id { get; set; }
    public Account Account { get; set; }
    public Guid AccountId { get; set; }
    public Stock Stock { get; set; }
    public string StockId { get; set; }
    public decimal Quantity { get; set; }
}