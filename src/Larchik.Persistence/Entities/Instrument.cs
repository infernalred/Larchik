namespace Larchik.Persistence.Entities;

public class Instrument
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public string Ticker { get; set; } = null!;
    // Instrument.CurrencyId is the default quote/nominal currency used for prices and valuation.
    // Operation.CurrencyId may differ because it represents the settlement currency of a concrete operation.
    public string? Isin { get; set; }
    public string? Figi { get; set; }
    public InstrumentType Type { get; set; }
    public string CurrencyId { get; set; } = null!;
    public int CategoryId { get; set; }
    public string? ExchangeId { get; set; }
    public string? CountryId { get; set; }
    public bool IsTrading { get; set; } = true;
    public PriceSource? PriceSource { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid CreatedBy { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public Guid UpdatedBy { get; set; }

    public Currency? Currency { get; set; }
    public Category? Category { get; set; }
    public Exchange? Exchange { get; set; }
    public Country? Country { get; set; }
    public ICollection<InstrumentListingHistory> ListingHistory { get; set; } = [];
}
