namespace Larchik.Persistence.Models;

public class Asset
{
    public Guid Id { get; set; }
    public Guid AccountId { get; set; }
    public Guid StockId { get; set; }
    public decimal Quantity { get; set; }
    public decimal AveragePrice { get; set; }
    public Guid UserId { get; set; }

    public Account? Account { get; set; }
    public Stock? Stock { get; set; }
}