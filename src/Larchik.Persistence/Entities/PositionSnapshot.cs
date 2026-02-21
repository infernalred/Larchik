namespace Larchik.Persistence.Entities;

public class PositionSnapshot
{
    public Guid Id { get; set; }
    public Guid PortfolioId { get; set; }
    public Guid InstrumentId { get; set; }
    public DateTime Date { get; set; }
    public decimal Quantity { get; set; }
    public decimal CostBase { get; set; }
    public decimal MarketValueBase { get; set; }
    public decimal UnrealizedBase { get; set; }
    public decimal RealizedBase { get; set; }

    public Portfolio? Portfolio { get; set; }
    public Instrument? Instrument { get; set; }
}
