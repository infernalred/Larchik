using Larchik.Application.Models;

namespace Larchik.Application.Portfolios;

internal static class PositionHoldingSortHelper
{
    public static void SortByAssetClass(List<PositionHoldingDto> positions) =>
        positions.Sort(CompareByAssetClass);

    private static int CompareByAssetClass(PositionHoldingDto left, PositionHoldingDto right)
    {
        var typeComparison = GetTypeOrder(left.InstrumentType).CompareTo(GetTypeOrder(right.InstrumentType));
        if (typeComparison != 0)
        {
            return typeComparison;
        }

        var nameComparison = string.Compare(left.InstrumentName, right.InstrumentName, StringComparison.CurrentCulture);
        if (nameComparison != 0)
        {
            return nameComparison;
        }

        return left.InstrumentId.CompareTo(right.InstrumentId);
    }

    private static int GetTypeOrder(string? instrumentType) =>
        instrumentType switch
        {
            nameof(Persistence.Entities.InstrumentType.Equity) => 0,
            nameof(Persistence.Entities.InstrumentType.Bond) => 1,
            nameof(Persistence.Entities.InstrumentType.Etf) => 2,
            nameof(Persistence.Entities.InstrumentType.Currency) => 3,
            nameof(Persistence.Entities.InstrumentType.Commodity) => 4,
            nameof(Persistence.Entities.InstrumentType.Crypto) => 5,
            _ => 99
        };
}
