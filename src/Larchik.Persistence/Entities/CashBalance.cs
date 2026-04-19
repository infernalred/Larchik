namespace Larchik.Persistence.Entities;

// Legacy entity kept only so historical EF migrations continue to compile.
// The active model no longer maps CashBalance.
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
