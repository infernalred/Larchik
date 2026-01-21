using Larchik.Persistence.Entities;

namespace Larchik.Application.Models;

public class InstrumentDto
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
}
