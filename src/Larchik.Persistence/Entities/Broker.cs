namespace Larchik.Persistence.Entities;

public class Broker
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public string? Country { get; set; }

    public ICollection<Portfolio> Portfolios { get; set; } = new List<Portfolio>();
}
