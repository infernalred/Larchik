namespace Larchik.Persistence.Entities;

public class Stock
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public string Ticker { get; set; } = null!;
    public string Isin { get; set; } = null!;
    public int Kind { get; set; }
    public string CurrencyId { get; set; } = null!;
    public int CategoryId { get; set; }
    public double Price { get; set; }
    public DateTime CreatedAt { get; set; }
    public Guid CreatedBy { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Guid UpdatedBy { get; set; }

    public Currency? Currency { get; set; }
    public Category? Category { get; set; }
}