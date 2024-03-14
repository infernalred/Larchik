using Larchik.Persistence.Enum;

namespace Larchik.Application.Dtos;

public class StockDto
{
    public Guid Id { get; set; }
    public string Ticker { get; init; } = null!;
    public string Isin { get; init; } = null!;
    public string Name { get; init; } = null!;
    public StockKind Kind { get; init; }
    public string CurrencyId { get; init; } = null!;
    public string SectorId { get; init; } = null!;
    public double LastPrice { get; init; }
}