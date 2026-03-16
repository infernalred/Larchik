namespace Larchik.Persistence.Entities;

public class Instrument
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public string Ticker { get; set; } = null!;
    public string Isin { get; set; } = null!;
    public string? Figi { get; set; }
    public InstrumentType Type { get; set; }
    public string CurrencyId { get; set; } = null!;
    public int CategoryId { get; set; }
    public string? Exchange { get; set; }
    public string? Country { get; set; }
    public bool IsTrading { get; set; } = true;
    public decimal? Price { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid CreatedBy { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public Guid UpdatedBy { get; set; }

    public Currency? Currency { get; set; }
    public Category? Category { get; set; }
    public ICollection<InstrumentListingHistory> ListingHistory { get; set; } = [];
}
