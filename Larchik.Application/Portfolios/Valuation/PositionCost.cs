namespace Larchik.Application.Portfolios.Valuation;

public class PositionCost
{
    public Guid InstrumentId { get; set; }
    public decimal Quantity { get; set; }
    public decimal RollingCost { get; set; }
    public decimal AverageCost => Quantity != 0 ? -RollingCost / Quantity : 0;
}
