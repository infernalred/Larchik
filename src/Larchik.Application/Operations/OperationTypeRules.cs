using Larchik.Persistence.Entities;

namespace Larchik.Application.Operations;

public static class OperationTypeRules
{
    public static bool RequiresInstrument(OperationType type) => type is
        OperationType.Buy or
        OperationType.Sell or
        OperationType.BondPartialRedemption or
        OperationType.BondMaturity or
        OperationType.Split or
        OperationType.ReverseSplit;
}
