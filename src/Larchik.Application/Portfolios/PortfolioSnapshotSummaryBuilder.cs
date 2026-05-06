using Larchik.Application.Helpers;
using Larchik.Application.Models;
using Larchik.Application.Portfolios.Valuation;
using Larchik.Persistence.Context;
using Larchik.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace Larchik.Application.Portfolios;

/// <summary>
/// Fast path for portfolio summary when persisted daily snapshots are current and valuation matches snapshot recalc (adjusting average).
/// </summary>
public static class PortfolioSnapshotSummaryBuilder
{
    private const string DefaultValuationMethod = "adjustingAvg";

    public static bool IsAdjustingAverageMethod(string? method) =>
        string.IsNullOrWhiteSpace(method) ||
        string.Equals(method, DefaultValuationMethod, StringComparison.OrdinalIgnoreCase);

    public static async Task<PortfolioSummaryDto?> TryBuildAsync(
        LarchikContext context,
        Portfolio portfolio,
        IReadOnlyList<Operation> mergedOperations,
        IReadOnlyDictionary<Guid, Instrument> instruments,
        HistoricalDataLookup data,
        string valuationMethod,
        string baseCurrency,
        DateTime asOfDateTime,
        bool includeAnnualizedReturn,
        CancellationToken cancellationToken)
    {
        if (!IsAdjustingAverageMethod(valuationMethod))
        {
            return null;
        }

        var latestSnapshotDate = await context.PortfolioSnapshots
            .Where(x => x.PortfolioId == portfolio.Id)
            .Select(x => (DateTime?)x.Date)
            .MaxAsync(cancellationToken);

        if (latestSnapshotDate is null)
        {
            return null;
        }

        var latestSnapDay = latestSnapshotDate.Value.Date;
        if (mergedOperations.Count > 0)
        {
            var maxOpDay = mergedOperations.Max(o => o.TradeDate).Date;
            if (maxOpDay > latestSnapDay)
            {
                return null;
            }
        }

        var asOfDay = asOfDateTime.Kind == DateTimeKind.Utc
            ? asOfDateTime.Date
            : asOfDateTime.ToUniversalTime().Date;

        if (latestSnapDay != asOfDay)
        {
            return null;
        }

        var effectiveDay = asOfDay;

        var portfolioSnap = await context.PortfolioSnapshots
            .Where(x => x.PortfolioId == portfolio.Id && x.Date == effectiveDay)
            .FirstOrDefaultAsync(cancellationToken);

        if (portfolioSnap is null)
        {
            return null;
        }

        var positionSnaps = await context.PositionSnapshots
            .Where(x => x.PortfolioId == portfolio.Id && x.Date == effectiveDay)
            .ToListAsync(cancellationToken);

        var accountingCurrencies = InstrumentAccountingCurrencyHelper.Build(mergedOperations, instruments, baseCurrency);
        var ledger = PortfolioLedgerAccumulator.Accumulate(portfolio, mergedOperations, instruments, data, baseCurrency, asOfDateTime);
        var netInflowBase = ledger.NetInflowBase;
        var grossDepositsBase = ledger.GrossDepositsBase;
        var grossWithdrawalsBase = ledger.GrossWithdrawalsBase;
        var (cashDtos, cashBase) = PortfolioLedgerAccumulator.BuildCashBalanceDtos(
            ledger.CashByCurrency,
            data,
            baseCurrency,
            asOfDateTime);

        var positionDtos = new List<PositionHoldingDto>();
        decimal positionsValueBase = 0;
        decimal costBasisBase = 0;
        var realizedInstrumentTotal = positionSnaps.Sum(x => x.RealizedBase);

        foreach (var snap in positionSnaps)
        {
            if (snap.Quantity == 0)
            {
                continue;
            }

            if (!instruments.TryGetValue(snap.InstrumentId, out var instrument))
            {
                continue;
            }

            if (instrument.Type == InstrumentType.Currency)
            {
                continue;
            }

            var price = data.GetPrice(snap.InstrumentId, asOfDateTime);
            var lastPrice = price?.Value;
            var quoteCurrency = price?.CurrencyId ?? instrument.CurrencyId;
            var accountingCurrency = InstrumentAccountingCurrencyHelper.Get(snap.InstrumentId, accountingCurrencies, instruments, baseCurrency);

            var marketValueBase = lastPrice.HasValue && snap.Quantity != 0
                ? data.Convert(snap.Quantity * lastPrice.Value, quoteCurrency, baseCurrency, asOfDateTime)
                : 0;

            var costBase = snap.CostBase;
            var avgCost = snap.Quantity != 0
                ? data.Convert(snap.CostBase / snap.Quantity, baseCurrency, accountingCurrency, asOfDateTime)
                : 0;

            positionDtos.Add(new PositionHoldingDto
            {
                InstrumentId = snap.InstrumentId,
                InstrumentName = instrument.Name,
                InstrumentType = instrument.Type.ToString(),
                CategoryName = instrument.Category?.Name,
                CurrencyId = quoteCurrency,
                PriceCurrencyId = quoteCurrency,
                AverageCostCurrencyId = accountingCurrency,
                Quantity = snap.Quantity,
                LastPrice = lastPrice,
                MarketValueBase = marketValueBase,
                AverageCost = avgCost
            });

            positionsValueBase += marketValueBase;
            costBasisBase += costBase;
        }

        PositionHoldingSortHelper.SortByAssetClass(positionDtos);

        var navBase = cashBase + positionsValueBase;
        var unrealizedBase = positionsValueBase - costBasisBase;

        decimal? annualizedReturnPct = null;
        if (includeAnnualizedReturn)
        {
            annualizedReturnPct = MoneyWeightedReturnCalculator.CalculateAnnualizedReturn(
                mergedOperations,
                data,
                baseCurrency,
                navBase,
                asOfDateTime);
        }

        return new PortfolioSummaryDto
        {
            Id = portfolio.Id,
            Name = portfolio.Name,
            ReportingCurrencyId = baseCurrency,
            NetInflowBase = netInflowBase,
            GrossDepositsBase = grossDepositsBase,
            GrossWithdrawalsBase = grossWithdrawalsBase,
            CashBase = cashBase,
            PositionsValueBase = positionsValueBase,
            RealizedBase = realizedInstrumentTotal,
            UnrealizedBase = unrealizedBase,
            PnlBase = navBase - netInflowBase,
            AnnualizedReturnPct = annualizedReturnPct,
            NavBase = navBase,
            ValuationMethod = valuationMethod,
            Cash = cashDtos,
            Positions = positionDtos,
            RealizedByInstrument = BuildRealizedDtos(positionSnaps, instruments, baseCurrency, accountingCurrencies, data, asOfDateTime)
        };
    }

    private static IReadOnlyCollection<RealizedPnlDto> BuildRealizedDtos(
        IReadOnlyList<PositionSnapshot> snaps,
        IReadOnlyDictionary<Guid, Instrument> instruments,
        string baseCurrency,
        IReadOnlyDictionary<Guid, string> accountingCurrencies,
        HistoricalDataLookup data,
        DateTime asOfDateTime)
    {
        var list = new List<RealizedPnlDto>();
        foreach (var g in snaps.GroupBy(x => x.InstrumentId))
        {
            var realizedBaseTotal = g.Sum(x => x.RealizedBase);
            if (realizedBaseTotal == 0)
            {
                continue;
            }

            var instrumentId = g.Key;
            var instrumentName = instruments.TryGetValue(instrumentId, out var instrument)
                ? instrument.Name
                : instrumentId.ToString();
            var accountingCurrency = InstrumentAccountingCurrencyHelper.Get(instrumentId, accountingCurrencies, instruments, baseCurrency);
            var realizedAccounting = data.Convert(realizedBaseTotal, baseCurrency, accountingCurrency, asOfDateTime);

            list.Add(new RealizedPnlDto
            {
                InstrumentId = instrumentId,
                InstrumentName = instrumentName,
                CurrencyId = accountingCurrency,
                Realized = realizedAccounting,
                RealizedBase = realizedBaseTotal
            });
        }

        list.Sort(static (a, b) => Math.Abs(b.RealizedBase).CompareTo(Math.Abs(a.RealizedBase)));
        return list;
    }
}
