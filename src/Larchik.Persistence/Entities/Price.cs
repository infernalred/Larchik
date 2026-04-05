namespace Larchik.Persistence.Entities;

public class Price
{
    public Guid Id { get; set; }
    public Guid InstrumentId { get; set; }
    public DateTime Date { get; set; }
    public decimal Value { get; set; }
    public string CurrencyId { get; set; } = null!;
    public string? SourceCurrencyId { get; set; }
    public string Provider { get; set; } = null!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public Instrument? Instrument { get; set; }
    public Currency? Currency { get; set; }
}
