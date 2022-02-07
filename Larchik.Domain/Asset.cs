namespace Larchik.Domain;

public class Asset
{
    public Guid Id { get; set; }
    public Account Account { get; set; }
    public Stock Stock { get; set; }
    public string StockId { get; set; }
    public int Quantity { get; set; }
    public ICollection<Transaction> Transactions { get; set; }
}