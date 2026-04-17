namespace Larchik.Application.Models;

public class PortfolioSummaryDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public string ReportingCurrencyId { get; set; } = null!;
    public decimal NetInflowBase { get; set; }
    public decimal GrossDepositsBase { get; set; }
    public decimal GrossWithdrawalsBase { get; set; }
    public decimal CashBase { get; set; }
    public decimal PositionsValueBase { get; set; }
    public decimal RealizedBase { get; set; }
    public decimal UnrealizedBase { get; set; }
    public decimal PnlBase { get; set; }
    public decimal? AnnualizedReturnPct { get; set; }
    public string? ValuationMethod { get; set; }
    public decimal NavBase { get; set; }
    public IReadOnlyCollection<CashBalanceDto> Cash { get; set; } = [];
    public IReadOnlyCollection<PositionHoldingDto> Positions { get; set; } = [];
    public IReadOnlyCollection<RealizedPnlDto> RealizedByInstrument { get; set; } = [];
}
