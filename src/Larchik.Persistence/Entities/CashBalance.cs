namespace Larchik.Persistence.Entities;

public class CashBalance
{
    public Guid Id { get; set; }
    public Guid PortfolioId { get; set; }
    public string CurrencyId { get; set; } = null!;
    public decimal Amount { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public Portfolio? Portfolio { get; set; }
    public Currency? Currency { get; set; }
}
