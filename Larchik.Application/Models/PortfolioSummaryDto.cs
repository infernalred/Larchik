using System.Collections.Generic;

namespace Larchik.Application.Models;

public class PortfolioSummaryDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public string ReportingCurrencyId { get; set; } = null!;
    public decimal NetInflowBase { get; set; }
    public decimal CashBase { get; set; }
    public decimal PositionsValueBase { get; set; }
    public decimal RealizedBase { get; set; }
    public decimal UnrealizedBase { get; set; }
    public string? ValuationMethod { get; set; }
    public decimal NavBase { get; set; }
    public IReadOnlyCollection<CashBalanceDto> Cash { get; set; } = Array.Empty<CashBalanceDto>();
    public IReadOnlyCollection<PositionHoldingDto> Positions { get; set; } = Array.Empty<PositionHoldingDto>();
    public IReadOnlyCollection<RealizedPnlDto> RealizedByInstrument { get; set; } = Array.Empty<RealizedPnlDto>();
}
