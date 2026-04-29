namespace Larchik.Persistence.Entities;

public class PortfolioReconciliationResult
{
    public Guid Id { get; set; }
    public Guid PortfolioId { get; set; }
    public DateTime StatementDate { get; set; }
    public string Source { get; set; } = null!;
    public string ReportingCurrencyId { get; set; } = null!;
    public string Status { get; set; } = null!;
    public string Severity { get; set; } = null!;
    public bool AlertRequired { get; set; }
    public string ReasonCode { get; set; } = null!;
    public decimal ToleranceBase { get; set; }
    public decimal ActualNavBase { get; set; }
    public decimal ActualCashBase { get; set; }
    public decimal ActualPositionsValueBase { get; set; }
    public decimal TargetNavBase { get; set; }
    public decimal TargetCashBase { get; set; }
    public decimal TargetPositionsValueBase { get; set; }
    public decimal NavDelta { get; set; }
    public decimal CashDelta { get; set; }
    public decimal PositionsDelta { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Portfolio? Portfolio { get; set; }
}
