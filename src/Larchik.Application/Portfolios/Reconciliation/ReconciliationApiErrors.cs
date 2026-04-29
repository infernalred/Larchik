namespace Larchik.Application.Portfolios.Reconciliation;

public static class ReconciliationApiErrors
{
    public const string InvalidSortByCode = "REC_INVALID_SORT_BY";
    public const string InvalidSortDirectionCode = "REC_INVALID_SORT_DIRECTION";
    public const string InvalidSeverityCode = "REC_INVALID_SEVERITY";

    public static string InvalidSortBy(string? value) =>
        $"{InvalidSortByCode}: Invalid sortBy '{value}'. Supported values: statementDate, createdAt, severity, status, navDelta.";

    public static string InvalidSortDirection(string? value) =>
        $"{InvalidSortDirectionCode}: Invalid sortDirection '{value}'. Supported values: asc, desc.";

    public static string InvalidSeverity(string? value) =>
        $"{InvalidSeverityCode}: Invalid severity '{value}'. Supported values: info, warning, critical.";
}
