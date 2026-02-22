namespace Larchik.Application.Models;

public class PortfoliosSummaryDto
{
    public string ReportingCurrencyId { get; set; } = null!;
    public int PortfolioCount { get; set; }
    public decimal NetInflowBase { get; set; }
    public decimal CashBase { get; set; }
    public decimal PositionsValueBase { get; set; }
    public decimal RealizedBase { get; set; }
    public decimal UnrealizedBase { get; set; }
    public decimal PnlBase { get; set; }
    public string? ValuationMethod { get; set; }
    public decimal NavBase { get; set; }
}
