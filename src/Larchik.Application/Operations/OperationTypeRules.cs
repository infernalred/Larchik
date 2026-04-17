using Larchik.Persistence.Entities;

namespace Larchik.Application.Operations;

public static class OperationTypeRules
{
    public static bool IsAdministrativeCorporateAction(OperationType type) => type is
        OperationType.Split or
        OperationType.ReverseSplit;

    public static bool IsVisibleInPortfolioOperations(OperationType type) => !IsAdministrativeCorporateAction(type);

    public static bool RequiresInstrument(OperationType type) => type is
        OperationType.Buy or
        OperationType.Sell or
        OperationType.Dividend or
        OperationType.BondPartialRedemption or
        OperationType.BondMaturity;

    public static bool RequiresPositiveQuantity(OperationType type) => type is
        OperationType.Buy or
        OperationType.Sell or
        OperationType.BondPartialRedemption or
        OperationType.BondMaturity;

    public static bool AllowsZeroQuantity(OperationType type) => !RequiresPositiveQuantity(type);
}
