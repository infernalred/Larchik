namespace Larchik.Application.Models;

public record PortfolioPerformanceDto
{
    public string Period { get; init; } = null!;
    public DateTime StartDate { get; init; }
    public DateTime EndDate { get; init; }
    public string ReportingCurrencyId { get; init; } = null!;
    public string ValuationMethod { get; init; } = "adjustingAvg";
    public decimal StartNavBase { get; init; }
    public decimal EndNavBase { get; init; }
    public decimal NetInflowBase { get; init; }
    public decimal PnlBase { get; init; }
    public decimal ReturnPct { get; init; }
    public decimal RealizedBase { get; init; }
    public decimal UnrealizedBase { get; init; }
}
