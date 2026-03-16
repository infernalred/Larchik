namespace Larchik.Persistence.Entities;

public class InstrumentListingHistory
{
    public Guid Id { get; set; }
    public Guid InstrumentId { get; set; }
    public string Ticker { get; set; } = null!;
    public string? Figi { get; set; }
    public string CurrencyId { get; set; } = null!;
    public string? Exchange { get; set; }
    public DateTime EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public Instrument? Instrument { get; set; }
    public Currency? Currency { get; set; }
}
