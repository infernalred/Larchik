namespace Larchik.Application.Dtos;

public class StockDto
{
    public string Ticker { get; init; } = null!;
    public string Figi { get; init; } = null!;
    public string CompanyName { get; init; } = null!;
    public string Type { get; init; } = null!;
    public string Currency { get; init; } = null!;
    public string Sector { get; init; } = null!;
    public double LastPrice { get; init; }
}