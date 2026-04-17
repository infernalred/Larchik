using Larchik.Persistence.Entities;

namespace Larchik.Application.Stocks.InstrumentCorporateActions;

public static class InstrumentCorporateActionRules
{
    public static bool IsSupportedType(OperationType type) => type is OperationType.Split or OperationType.ReverseSplit;

    public static DateTime NormalizeEffectiveDate(DateTimeOffset value) => value.UtcDateTime.Date;
}
