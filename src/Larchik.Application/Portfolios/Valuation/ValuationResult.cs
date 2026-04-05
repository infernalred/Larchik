namespace Larchik.Application.Portfolios.Valuation;

public class ValuationResult
{
    public Dictionary<Guid, PositionCost> Positions { get; } = new();
    public Dictionary<Guid, decimal> RealizedByInstrument { get; } = new();
}
