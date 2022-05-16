namespace Larchik.Infrastructure.Market;

public class LastPriceRequest
{
    public IEnumerable<string> Figi { get; set; } = null!;
}