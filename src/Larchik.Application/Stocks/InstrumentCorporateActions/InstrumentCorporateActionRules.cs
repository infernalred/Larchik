using Larchik.Persistence.Entities;

namespace Larchik.Application.Stocks.InstrumentCorporateActions;

public static class InstrumentCorporateActionRules
{
    public static bool IsSupportedType(OperationType type) => type is OperationType.Split or OperationType.ReverseSplit;

    public static bool IsValidFactor(OperationType type, decimal factor) => type switch
    {
        OperationType.Split => factor > 1m,
        OperationType.ReverseSplit => factor > 0m && factor < 1m,
        _ => false
    };

    public static bool IsSupportedInstrumentType(InstrumentType type) => type is InstrumentType.Equity or InstrumentType.Etf;

    public static DateTime NormalizeEffectiveDate(DateTimeOffset value) => value.UtcDateTime.Date;
}
