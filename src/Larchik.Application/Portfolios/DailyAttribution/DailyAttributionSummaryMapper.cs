using Larchik.Application.Models;

namespace Larchik.Application.Portfolios.DailyAttribution;

public static class DailyAttributionSummaryMapper
{
    public static void Attach(PortfolioSummaryDto summary, DailyPnlAttributionDto attribution)
    {
        summary.DailyMove = ToPortfolioMove(attribution);

        var positionMoves = attribution.Positions.ToDictionary(x => x.InstrumentId);
        foreach (var position in summary.Positions)
        {
            position.DailyMove = positionMoves.TryGetValue(position.InstrumentId, out var move)
                ? ToMove(move)
                : null;
        }

        var cashMoves = attribution.Cash.ToDictionary(x => x.CurrencyId, StringComparer.OrdinalIgnoreCase);
        foreach (var cash in summary.Cash)
        {
            cash.DailyMove = cashMoves.TryGetValue(cash.CurrencyId, out var move)
                ? ToMove(move)
                : null;
        }
    }

    public static DailyMoveDto? Aggregate(IEnumerable<DailyMoveDto?> source)
    {
        var items = source.ToArray();
        var moves = items.Where(x => x is not null).Cast<DailyMoveDto>().ToArray();
        if (moves.Length == 0)
        {
            return null;
        }

        var startValueBase = moves.Sum(x => x.StartValueBase);
        var pnlBase = moves.Sum(x => x.PnlBase);
        return new DailyMoveDto
        {
            StartValueBase = startValueBase,
            PnlBase = pnlBase,
            ReturnPct = startValueBase == 0m ? null : pnlBase / startValueBase,
            PriceEffectBase = moves.Sum(x => x.PriceEffectBase),
            FxEffectBase = moves.Sum(x => x.FxEffectBase),
            CrossEffectBase = moves.Sum(x => x.CrossEffectBase),
            TradingEffectBase = moves.Sum(x => x.TradingEffectBase),
            IncomeEffectBase = moves.Sum(x => x.IncomeEffectBase),
            FeeEffectBase = moves.Sum(x => x.FeeEffectBase),
            OtherEffectBase = moves.Sum(x => x.OtherEffectBase),
            DataQuality = moves.Length == items.Length && moves.All(x => x.DataQuality == "complete")
                ? "complete"
                : "partial"
        };
    }

    public static PortfolioDailyMoveDto? AggregatePortfolios(IEnumerable<PortfolioDailyMoveDto?> source)
    {
        var items = source.ToArray();
        var moves = items.Where(x => x is not null).Cast<PortfolioDailyMoveDto>().ToArray();
        if (moves.Length == 0)
        {
            return null;
        }

        var aggregate = Aggregate(items)!;
        return new PortfolioDailyMoveDto
        {
            ComparisonDate = moves.Min(x => x.ComparisonDate),
            ValuationDate = moves.Max(x => x.ValuationDate),
            StartValueBase = aggregate.StartValueBase,
            PnlBase = aggregate.PnlBase,
            ReturnPct = aggregate.ReturnPct,
            PriceEffectBase = aggregate.PriceEffectBase,
            FxEffectBase = aggregate.FxEffectBase,
            CrossEffectBase = aggregate.CrossEffectBase,
            TradingEffectBase = aggregate.TradingEffectBase,
            IncomeEffectBase = aggregate.IncomeEffectBase,
            FeeEffectBase = aggregate.FeeEffectBase,
            OtherEffectBase = aggregate.OtherEffectBase,
            DataQuality = aggregate.DataQuality
        };
    }

    private static PortfolioDailyMoveDto ToPortfolioMove(DailyPnlAttributionDto source) => new()
    {
        ComparisonDate = source.ComparisonDate,
        ValuationDate = source.ValuationDate,
        StartValueBase = source.StartNavBase,
        PnlBase = source.PnlBase,
        ReturnPct = source.ReturnPct,
        PriceEffectBase = source.PriceEffectBase,
        FxEffectBase = source.FxEffectBase,
        CrossEffectBase = source.CrossEffectBase,
        TradingEffectBase = source.TradingEffectBase,
        IncomeEffectBase = source.IncomeEffectBase,
        FeeEffectBase = source.FeeEffectBase,
        OtherEffectBase = source.OtherEffectBase,
        DataQuality = source.IsComplete ? "complete" : "partial"
    };

    private static DailyMoveDto ToMove(PositionDailyPnlAttributionDto source) => new()
    {
        StartValueBase = source.StartMarketValueBase,
        PnlBase = source.PnlBase,
        ReturnPct = source.ReturnPct,
        PriceEffectBase = source.PriceEffectBase,
        FxEffectBase = source.FxEffectBase,
        CrossEffectBase = source.CrossEffectBase,
        TradingEffectBase = source.TradingEffectBase,
        IncomeEffectBase = source.IncomeEffectBase,
        FeeEffectBase = source.FeeEffectBase,
        OtherEffectBase = source.OtherEffectBase,
        DataQuality = source.DataQuality
    };

    private static DailyMoveDto ToMove(CashDailyPnlAttributionDto source)
    {
        var startValueBase = source.StartAmount * (source.StartFxRate ?? 1m);
        return new DailyMoveDto
        {
            StartValueBase = startValueBase,
            PnlBase = source.FxEffectBase,
            ReturnPct = startValueBase == 0m ? null : source.FxEffectBase / startValueBase,
            FxEffectBase = source.FxEffectBase,
            DataQuality = source.DataQuality
        };
    }
}
