namespace Larchik.Application.Models;

public record RecalculatePortfolioResultDto(
    DateTime RecalculatedFromDate,
    int OperationCount);
