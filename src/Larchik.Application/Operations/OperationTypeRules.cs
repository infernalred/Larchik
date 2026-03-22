using Larchik.Persistence.Entities;

namespace Larchik.Application.Operations;

public static class OperationTypeRules
{
    public static bool RequiresInstrument(OperationType type) => type is
        OperationType.Buy or
        OperationType.Sell or
        OperationType.Dividend or
        OperationType.BondPartialRedemption or
        OperationType.BondMaturity or
        OperationType.Split or
        OperationType.ReverseSplit;

    public static bool RequiresPositiveQuantity(OperationType type) => type is
        OperationType.Buy or
        OperationType.Sell or
        OperationType.BondPartialRedemption or
        OperationType.BondMaturity;

    public static bool AllowsZeroQuantity(OperationType type) => !RequiresPositiveQuantity(type);
}
