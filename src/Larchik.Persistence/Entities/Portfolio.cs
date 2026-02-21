namespace Larchik.Persistence.Entities;

public class Portfolio
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid BrokerId { get; set; }
    public string Name { get; set; } = null!;
    public string ReportingCurrencyId { get; set; } = null!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Broker? Broker { get; set; }
    public AppUser? User { get; set; }
    public ICollection<Operation> Operations { get; set; } = new List<Operation>();
    public ICollection<CashBalance> CashBalances { get; set; } = new List<CashBalance>();
    public ICollection<PositionSnapshot> PositionSnapshots { get; set; } = new List<PositionSnapshot>();
    public ICollection<PortfolioSnapshot> PortfolioSnapshots { get; set; } = new List<PortfolioSnapshot>();
}
