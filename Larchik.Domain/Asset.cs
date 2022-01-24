namespace Larchik.Domain;

public class Asset
{
    public Guid Id { get; set; }
    public Stock Stock { get; set; }
    public int Quantity { get; set; }
}