namespace Larchik.Persistence.Entities;

public class MarketDataImportRequest
{
    public Guid Id { get; set; }
    public Guid RequestedBy { get; set; }
    public PriceSource Source { get; set; }
    public string Isin { get; set; } = null!;
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public DateTime NextDate { get; set; }
    public MarketDataImportStatus Status { get; set; } = MarketDataImportStatus.Queued;
    public Guid? InstrumentId { get; set; }
    public int InsertedPrices { get; set; }
    public int UpdatedPrices { get; set; }
    public int Attempt { get; set; }
    public string? LastError { get; set; }
    public string? IdempotencyKey { get; set; }
    public string? SourceInstrumentCode { get; set; }
    public string? SourceBoard { get; set; }
    public string? SourceEngine { get; set; }
    public string? SourceMarket { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public Instrument? Instrument { get; set; }
}
