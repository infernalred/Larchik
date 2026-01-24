namespace Larchik.Application.Stocks.GetStock;

public class PositionCost
{
    public Guid InstrumentId { get; set; }
    public decimal Quantity { get; set; }
    public decimal RollingCost { get; set; }
    public decimal AverageCost => Quantity != 0 ? -RollingCost / Quantity : 0;
}
