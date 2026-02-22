namespace Larchik.Application.Models;

public class InstrumentLookupDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public string Ticker { get; set; } = null!;
    public string Isin { get; set; } = null!;
    public string? Figi { get; set; }
    public string CurrencyId { get; set; } = null!;
}
