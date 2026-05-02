using Larchik.Application.Models;
using Larchik.Application.Portfolios.Valuation;
using Larchik.Application.Helpers;
using Larchik.Persistence.Entities;

namespace Larchik.Application.Portfolios;

public sealed class PortfolioAnalyticsCalculator
{
    public PortfolioSummaryDto CalculateSummary(
        Portfolio portfolio,
        IReadOnlyList<Operation> operations,
        IReadOnlyDictionary<Guid, Instrument> instruments,
        HistoricalDataLookup data,
        string valuationMethod,
        string baseCurrency,
        DateTime asOfDate,
        bool includeAnnualizedReturn = true)
    {
        var acc = PortfolioLedgerAccumulator.Accumulate(portfolio, operations, instruments, data, baseCurrency, asOfDate);
        var cashByCurrency = acc.CashByCurrency;
        var positions = acc.Positions;
        var valuationOperations = acc.ValuationOperations;
        var netInflowBase = acc.NetInflowBase;
        var grossDepositsBase = acc.GrossDepositsBase;
        var grossWithdrawalsBase = acc.GrossWithdrawalsBase;
        var accountingCurrencies = InstrumentAccountingCurrencyHelper.Build(operations, instruments, baseCurrency);

        var valuation = new ValuationService().Evaluate(valuationOperations, valuationMethod, assumeSorted: true);
        var positionCosts = valuation.Positions;

        var (cashDtos, cashBase) = PortfolioLedgerAccumulator.BuildCashBalanceDtos(cashByCurrency, data, baseCurrency, asOfDate);

        var positionDtos = new List<PositionHoldingDto>();
        var positionsValueBase = 0m;
        var costBasisBase = 0m;
        foreach (var kvp in positions)
        {
            if (kvp.Value == 0 || !instruments.TryGetValue(kvp.Key, out var instrument))
            {
                continue;
            }

            positionCosts.TryGetValue(kvp.Key, out var cost);
            var price = data.GetPrice(kvp.Key, asOfDate);
            var lastPrice = price?.Value;
            var quoteCurrency = price?.CurrencyId ?? instrument.CurrencyId;
            var accountingCurrency = InstrumentAccountingCurrencyHelper.Get(kvp.Key, accountingCurrencies, instruments, baseCurrency);
            var marketValueBase = lastPrice.HasValue
                ? data.Convert(kvp.Value * lastPrice.Value, quoteCurrency, baseCurrency, asOfDate)
                : 0;
            var avgCost = cost?.AverageCost ?? 0;
            var costBase = data.Convert(avgCost * kvp.Value, accountingCurrency, baseCurrency, asOfDate);

            positionDtos.Add(new PositionHoldingDto
            {
                InstrumentId = kvp.Key,
                InstrumentName = instrument.Name,
                InstrumentType = instrument.Type.ToString(),
                CategoryName = instrument.Category?.Name,
                CurrencyId = quoteCurrency,
                PriceCurrencyId = quoteCurrency,
                AverageCostCurrencyId = accountingCurrency,
                Quantity = kvp.Value,
                LastPrice = lastPrice,
                MarketValueBase = marketValueBase,
                AverageCost = avgCost
            });

            positionsValueBase += marketValueBase;
            costBasisBase += costBase;
        }

        var realizedBase = 0m;
        var realizedDtos = new List<RealizedPnlDto>();
        foreach (var kvp in valuation.RealizedByInstrument)
        {
            var instrumentName = instruments.TryGetValue(kvp.Key, out var instrument)
                ? instrument.Name
                : kvp.Key.ToString();
            var accountingCurrency = InstrumentAccountingCurrencyHelper.Get(kvp.Key, accountingCurrencies, instruments, baseCurrency);
            var realizedBaseValue = data.Convert(kvp.Value, accountingCurrency, baseCurrency, asOfDate);
            realizedBase += realizedBaseValue;

            realizedDtos.Add(new RealizedPnlDto
            {
                InstrumentId = kvp.Key,
                InstrumentName = instrumentName,
                CurrencyId = accountingCurrency,
                Realized = kvp.Value,
                RealizedBase = realizedBaseValue
            });
        }

        var navBase = cashBase + positionsValueBase;
        decimal? annualizedReturnPct = null;
        if (includeAnnualizedReturn)
        {
            annualizedReturnPct = MoneyWeightedReturnCalculator.CalculateAnnualizedReturn(
                operations,
                data,
                baseCurrency,
                navBase,
                asOfDate);
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
            RealizedBase = realizedBase,
            UnrealizedBase = positionsValueBase - costBasisBase,
            PnlBase = navBase - netInflowBase,
            AnnualizedReturnPct = annualizedReturnPct,
            NavBase = navBase,
            ValuationMethod = valuationMethod,
            Cash = cashDtos,
            Positions = positionDtos,
            RealizedByInstrument = realizedDtos
        };
    }

    public IReadOnlyCollection<PortfolioPerformanceDto> CalculatePerformance(
        Portfolio portfolio,
        IReadOnlyList<Operation> operations,
        IReadOnlyDictionary<Guid, Instrument> instruments,
        HistoricalDataLookup data,
        string valuationMethod,
        string baseCurrency,
        DateTime? from = null,
        DateTime? to = null)
    {
        if (operations.Count == 0)
        {
            return [];
        }

        var fromDate = from?.Date ?? operations.First().TradeDate.Date;
        var toDate = to?.Date ?? DateTime.UtcNow.Date;
        var cursor = new DateTime(fromDate.Year, fromDate.Month, 1);
        var lastMonthEnd = new DateTime(toDate.Year, toDate.Month, DateTime.DaysInMonth(toDate.Year, toDate.Month));
        var results = new List<PortfolioPerformanceDto>();

        while (cursor <= lastMonthEnd)
        {
            var monthEnd = new DateTime(cursor.Year, cursor.Month, DateTime.DaysInMonth(cursor.Year, cursor.Month));
            if (monthEnd > lastMonthEnd)
            {
                monthEnd = lastMonthEnd;
            }

            var startBoundary = cursor.AddDays(-1);
            var startSnapshot = CalculateSummary(portfolio, operations, instruments, data, valuationMethod, baseCurrency, startBoundary, includeAnnualizedReturn: false);
            var endSnapshot = CalculateSummary(portfolio, operations, instruments, data, valuationMethod, baseCurrency, monthEnd, includeAnnualizedReturn: false);
            var netFlow = ComputeFlows(operations, data, baseCurrency, cursor, monthEnd);

            if (endSnapshot.NavBase == 0 && startSnapshot.NavBase == 0 && netFlow == 0)
            {
                cursor = cursor.AddMonths(1);
                continue;
            }

            var pnl = endSnapshot.NavBase - startSnapshot.NavBase - netFlow;
            var returnPct = startSnapshot.NavBase != 0 ? pnl / startSnapshot.NavBase : 0m;

            results.Add(new PortfolioPerformanceDto
            {
                Period = $"{cursor:yyyy-MM}",
                StartDate = cursor,
                EndDate = monthEnd,
                ReportingCurrencyId = baseCurrency,
                ValuationMethod = valuationMethod,
                StartNavBase = startSnapshot.NavBase,
                EndNavBase = endSnapshot.NavBase,
                NetInflowBase = netFlow,
                PnlBase = pnl,
                ReturnPct = returnPct,
                RealizedBase = endSnapshot.RealizedBase - startSnapshot.RealizedBase,
                UnrealizedBase = endSnapshot.UnrealizedBase
            });

            cursor = cursor.AddMonths(1);
        }

        return results;
    }

    private static decimal ComputeFlows(
        IEnumerable<Operation> operations,
        HistoricalDataLookup data,
        string baseCurrency,
        DateTime fromInclusive,
        DateTime toInclusive)
    {
        var flow = 0m;
        foreach (var op in operations)
        {
            var date = op.TradeDate.Date;
            if (date < fromInclusive.Date || date > toInclusive.Date)
            {
                continue;
            }

            var amount = op.Price != 0 ? op.Price : op.Quantity;
            switch (op.Type)
            {
                case OperationType.Deposit:
                    flow += data.Convert(amount, op.CurrencyId, baseCurrency, op.TradeDate);
                    break;
                case OperationType.Withdraw:
                    flow -= data.Convert(amount, op.CurrencyId, baseCurrency, op.TradeDate);
                    break;
                case OperationType.TransferIn when op.InstrumentId == null:
                    flow += data.Convert(amount, op.CurrencyId, baseCurrency, op.TradeDate);
                    break;
                case OperationType.TransferOut when op.InstrumentId == null:
                    flow -= data.Convert(amount, op.CurrencyId, baseCurrency, op.TradeDate);
                    break;
            }
        }

        return flow;
    }
}
