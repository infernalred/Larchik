namespace Larchik.Persistence.Entities;

public class Lot
{
    public Guid Id { get; set; }
    public Guid PortfolioId { get; set; }
    public Guid InstrumentId { get; set; }
    public ValuationMethod Method { get; set; }
    public decimal Quantity { get; set; }
    public decimal RemainingQuantity { get; set; }
    public decimal Cost { get; set; }
    public string CurrencyId { get; set; } = null!;
    public DateTime OpenedAt { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public Portfolio? Portfolio { get; set; }
    public Instrument? Instrument { get; set; }
    public Currency? Currency { get; set; }
}
