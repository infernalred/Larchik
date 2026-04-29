namespace Larchik.Application.Models;

public class PortfolioReconciliationAlertsSummaryDto
{
    public int TotalAlerts { get; set; }
    public int CriticalAlerts { get; set; }
    public int WarningAlerts { get; set; }
    public IReadOnlyCollection<PortfolioReconciliationResultDto> LatestCriticalByPortfolio { get; set; } = [];
}
