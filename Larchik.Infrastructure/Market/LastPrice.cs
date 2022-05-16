namespace Larchik.Infrastructure.Market;

public class LastPrice
{
    public string Figi { get; set; } = null!;
    public Quotation Price { get; set; } = null!;
}