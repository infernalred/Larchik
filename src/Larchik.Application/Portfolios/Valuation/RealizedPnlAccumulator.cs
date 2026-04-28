namespace Larchik.Application.Portfolios.Valuation;

internal static class RealizedPnlAccumulator
{
    public static void Add(ValuationResult result, Guid instrumentId, decimal realized)
    {
        if (realized == 0)
        {
            return;
        }

        if (result.RealizedByInstrument.TryGetValue(instrumentId, out var existing))
        {
            result.RealizedByInstrument[instrumentId] = existing + realized;
            return;
        }

        result.RealizedByInstrument[instrumentId] = realized;
    }
}
