using Larchik.Application.Portfolios.Valuation;

namespace Larchik.Application.Portfolios;

public static class PositionMarketValueHelper
{
    public static decimal CalculateMarketValueBase(
        decimal quantity,
        decimal? lastPrice,
        string quoteCurrency,
        decimal averageCost,
        string accountingCurrency,
        HistoricalDataLookup data,
        string baseCurrency,
        DateTime asOfDate)
    {
        if (quantity == 0)
        {
            return 0m;
        }

        return lastPrice.HasValue
            ? data.Convert(quantity * lastPrice.Value, quoteCurrency, baseCurrency, asOfDate)
            : data.Convert(quantity * averageCost, accountingCurrency, baseCurrency, asOfDate);
    }
}

