namespace Larchik.Application.Models;

public record ClearPortfolioDataResultDto(
    int DeletedOperations,
    int DeletedPositionSnapshots,
    int DeletedPortfolioSnapshots,
    int DeletedLots,
    int DeletedCashBalances);
