namespace Larchik.Persistence.Entities;

public class FxRate
{
    public Guid Id { get; set; }
    public string BaseCurrencyId { get; set; } = null!;
    public string QuoteCurrencyId { get; set; } = null!;
    public DateTime Date { get; set; }
    public decimal Rate { get; set; }
    public string Source { get; set; } = null!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Currency? BaseCurrency { get; set; }
    public Currency? QuoteCurrency { get; set; }
}
