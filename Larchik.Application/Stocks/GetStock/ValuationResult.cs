namespace Larchik.Application.Stocks.GetStock;

public class ValuationResult
{
    public Dictionary<Guid, PositionCost> Positions { get; } = new();
    public Dictionary<Guid, decimal> RealizedByInstrument { get; } = new();
}
