using Larchik.Persistence.Entities;

namespace Larchik.Application.Stocks;

public static class InstrumentRules
{
    public static bool RequiresIsin(InstrumentType type) => type is
        InstrumentType.Equity or
        InstrumentType.Bond or
        InstrumentType.Etf;
}
