namespace Larchik.Infrastructure.Jobs;

internal static class ReconciliationTargetDateHelper
{
    public static bool ShouldIncludeTarget(PortfolioReconciliationTargetOptions target, DateOnly runDate)
    {
        if (string.IsNullOrWhiteSpace(target.Date))
        {
            return true;
        }

        return DateOnly.TryParse(target.Date, out var targetDate) && targetDate == runDate;
    }
}
