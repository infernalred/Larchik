using Larchik.Application.Portfolios.Valuation;
using Larchik.Persistence.Entities;

namespace Larchik.Application.Portfolios;

internal readonly record struct ExternalCashFlow(DateTime Date, decimal AmountBase);

internal static class MoneyWeightedReturnCalculator
{
    private const double DaysPerYear = 365.2425d;
    private const double MinRate = -0.999999d;
    private const double MaxRate = 1024d;
    private const double Epsilon = 1e-10d;

    public static decimal? CalculateAnnualizedReturn(
        IReadOnlyList<Operation> operations,
        HistoricalDataLookup data,
        string baseCurrency,
        decimal navBase,
        DateTime asOfDate)
    {
        var cashFlows = new List<ExternalCashFlow>();

        foreach (var op in operations)
        {
            if (op.TradeDate > asOfDate)
            {
                break;
            }

            var amount = op.Price != 0 ? op.Price : op.Quantity;
            var amountBase = data.Convert(amount, op.CurrencyId, baseCurrency, op.TradeDate);

            switch (op.Type)
            {
                case OperationType.Deposit:
                    cashFlows.Add(new ExternalCashFlow(op.TradeDate.Date, -amountBase));
                    break;
                case OperationType.Withdraw:
                    cashFlows.Add(new ExternalCashFlow(op.TradeDate.Date, amountBase));
                    break;
                case OperationType.TransferIn when op.InstrumentId is null:
                    cashFlows.Add(new ExternalCashFlow(op.TradeDate.Date, -amountBase));
                    break;
                case OperationType.TransferOut when op.InstrumentId is null:
                    cashFlows.Add(new ExternalCashFlow(op.TradeDate.Date, amountBase));
                    break;
            }
        }

        return CalculateAnnualizedReturn(cashFlows, navBase, asOfDate);
    }

    public static decimal? CalculateAnnualizedReturn(
        IReadOnlyCollection<ExternalCashFlow> cashFlows,
        decimal navBase,
        DateTime asOfDate)
    {
        var normalizedFlows = cashFlows
            .Where(x => x.AmountBase != 0)
            .Select(x => new ExternalCashFlow(x.Date.Date, x.AmountBase))
            .ToList();

        if (navBase != 0)
        {
            normalizedFlows.Add(new ExternalCashFlow(asOfDate.Date, navBase));
        }

        if (normalizedFlows.Count < 2)
        {
            return null;
        }

        var firstDate = normalizedFlows.Min(x => x.Date);
        var lastDate = normalizedFlows.Max(x => x.Date);
        if ((lastDate - firstDate).TotalDays < 30)
        {
            return null;
        }

        var hasNegative = normalizedFlows.Any(x => x.AmountBase < 0);
        var hasPositive = normalizedFlows.Any(x => x.AmountBase > 0);
        if (!hasNegative || !hasPositive)
        {
            return null;
        }

        var schedule = normalizedFlows
            .Select(x => (
                Years: (x.Date - firstDate).TotalDays / DaysPerYear,
                Amount: (double)x.AmountBase))
            .OrderBy(x => x.Years)
            .ToArray();

        var rate = Solve(schedule);
        return rate is null ? null : (decimal)rate.Value;
    }

    private static double? Solve(IReadOnlyList<(double Years, double Amount)> cashFlows)
    {
        var low = MinRate;
        var high = 0.10d;
        var fLow = Npv(low, cashFlows);
        var fHigh = Npv(high, cashFlows);

        if (Math.Abs(fLow) < Epsilon)
        {
            return low;
        }

        if (Math.Abs(fHigh) < Epsilon)
        {
            return high;
        }

        while (Math.Sign(fLow) == Math.Sign(fHigh) && high < MaxRate)
        {
            high *= 2d;
            fHigh = Npv(high, cashFlows);

            if (Math.Abs(fHigh) < Epsilon)
            {
                return high;
            }
        }

        if (Math.Sign(fLow) == Math.Sign(fHigh))
        {
            return null;
        }

        for (var i = 0; i < 256; i++)
        {
            var mid = (low + high) / 2d;
            var fMid = Npv(mid, cashFlows);

            if (Math.Abs(fMid) < Epsilon)
            {
                return mid;
            }

            if (Math.Sign(fLow) == Math.Sign(fMid))
            {
                low = mid;
                fLow = fMid;
            }
            else
            {
                high = mid;
            }
        }

        return (low + high) / 2d;
    }

    private static double Npv(double rate, IReadOnlyList<(double Years, double Amount)> cashFlows)
    {
        var baseFactor = 1d + rate;
        if (baseFactor <= 0d)
        {
            return double.NaN;
        }

        var result = 0d;
        foreach (var (years, amount) in cashFlows)
        {
            result += amount / Math.Pow(baseFactor, years);
        }

        return result;
    }
}
