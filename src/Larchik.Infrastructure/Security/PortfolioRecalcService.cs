using Larchik.Application.Contracts;
using Larchik.Application.Portfolios.Valuation;
using Larchik.Persistence.Context;
using Larchik.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Larchik.Infrastructure.Recalculation;

public class PortfolioRecalcService(LarchikContext context, ILogger<PortfolioRecalcService> logger)
    : IPortfolioRecalcService
{
    public async Task ScheduleRebuild(Guid portfolioId, DateTime fromDate, CancellationToken cancellationToken = default)
    {
        var portfolio = await context.Portfolios
            .Include(x => x.Broker)
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == portfolioId, cancellationToken);

        if (portfolio is null)
        {
            logger.LogWarning("Portfolio {PortfolioId} not found for recalc", portfolioId);
            return;
        }

        var operations = await context.Operations
            .Where(x => x.PortfolioId == portfolioId)
            .OrderBy(x => x.TradeDate)
            .ThenBy(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

        if (operations.Count == 0)
        {
            await RemoveSnapshots(portfolioId, fromDate.Date, cancellationToken);
            return;
        }

        var baseCurrency = portfolio.ReportingCurrencyId;
        var instrumentIds = operations
            .Where(x => x.InstrumentId != null)
            .Select(x => x.InstrumentId!.Value)
            .Distinct()
            .ToArray();

        var instruments = await context.Instruments
            .Where(x => instrumentIds.Contains(x.Id))
            .AsNoTracking()
            .ToDictionaryAsync(x => x.Id, cancellationToken);

        var prices = await context.Prices
            .AsNoTracking()
            .Where(x => instrumentIds.Contains(x.InstrumentId))
            .ToListAsync(cancellationToken);

        var neededCurrencies = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { baseCurrency };
        foreach (var op in operations) neededCurrencies.Add(op.CurrencyId);
        foreach (var instrument in instruments.Values) neededCurrencies.Add(instrument.CurrencyId);

        var fxRates = await MarketFxRateLoader.LoadAsync(context, neededCurrencies, cancellationToken);

        var data = new HistoricalDataLookup(prices, fxRates);
        var accountingCurrencies = InstrumentAccountingCurrencyHelper.Build(operations, instruments, baseCurrency);

        var earliestOpDate = operations.First().TradeDate.Date;
        var persistFrom = fromDate.Date < earliestOpDate ? earliestOpDate : fromDate.Date;
        var calculationStart = earliestOpDate;
        var calculationEnd = DateTime.UtcNow.Date;

        await RemoveSnapshots(portfolioId, persistFrom, cancellationToken);

        var valuationOperations = new List<ValuationOperation>();
        foreach (var op in operations.Where(o => o.InstrumentId != null))
        {
            var instrumentCurrency = InstrumentAccountingCurrencyHelper.Get(op.InstrumentId!.Value, accountingCurrencies, instruments, baseCurrency);
            instruments.TryGetValue(op.InstrumentId!.Value, out var instrument);
            if (instrument?.Type == InstrumentType.Currency)
            {
                continue;
            }
            var price = data.Convert(op.Price, op.CurrencyId, instrumentCurrency, op.TradeDate);
            var fee = data.Convert(op.Fee, op.CurrencyId, instrumentCurrency, op.TradeDate);

            valuationOperations.Add(new ValuationOperation(
                op.InstrumentId!.Value,
                op.Type,
                op.Quantity,
                price,
                fee,
                op.TradeDate,
                op.CreatedAt));
        }

        var valuationService = new ValuationService();
        var usesBrokerCashLedger = BrokerCashLedgerHelper.UsesBrokerCashLedger(portfolio);
        var cashByCurrency = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        var netFlowByDate = new Dictionary<DateTime, decimal>();
        var portfolioSnapshots = new List<PortfolioSnapshot>();
        var positionSnapshots = new List<PositionSnapshot>();

        var positionOpIndex = 0;
        var cashOperations = operations
            .OrderBy(BrokerCashLedgerHelper.GetCashEffectiveDate)
            .ThenBy(x => x.CreatedAt)
            .ToList();
        var cashOpIndex = 0;
        var valuationCount = 0;
        var prevNav = 0m;

        for (var date = calculationStart; date <= calculationEnd; date = date.AddDays(1))
        {
            while (positionOpIndex < operations.Count && operations[positionOpIndex].TradeDate.Date <= date)
            {
                var op = operations[positionOpIndex++];
                TrackFlows(op, baseCurrency, op.TradeDate, data, netFlowByDate);
            }

            while (cashOpIndex < cashOperations.Count && BrokerCashLedgerHelper.GetCashEffectiveDate(cashOperations[cashOpIndex]) <= date)
            {
                var op = cashOperations[cashOpIndex++];
                ApplyCash(op, cashByCurrency, usesBrokerCashLedger, date);
            }

            while (valuationCount < valuationOperations.Count && valuationOperations[valuationCount].TradeDate.Date <= date)
            {
                valuationCount++;
            }

            var valuation = valuationService.Evaluate(valuationOperations.Take(valuationCount), null, assumeSorted: true);
            var positionsValueBase = 0m;
            var costBaseTotal = 0m;
            var realizedBaseTotal = 0m;
            var dailyPositionSnapshots = new List<PositionSnapshot>();

            foreach (var kvp in valuation.Positions)
            {
                var instrumentId = kvp.Key;
                var position = kvp.Value;
                var qty = position.Quantity;
                var instrumentCurrency = InstrumentAccountingCurrencyHelper.Get(instrumentId, accountingCurrencies, instruments, baseCurrency);
                instruments.TryGetValue(instrumentId, out var instrument);
                if (instrument?.Type == InstrumentType.Currency)
                {
                    continue;
                }
                var price = data.GetPrice(instrumentId, date);
                var lastPrice = price?.Value;
                var quoteCurrency = price?.CurrencyId ?? instrumentCurrency;
                var marketValueBase = lastPrice.HasValue
                    ? data.Convert(qty * lastPrice.Value, quoteCurrency, baseCurrency, date)
                    : 0m;
                var avgCost = position.AverageCost;
                var costBase = data.Convert(avgCost * qty, instrumentCurrency, baseCurrency, date);
                var realizedBase = valuation.RealizedByInstrument.TryGetValue(instrumentId, out var realized)
                    ? data.Convert(realized, instrumentCurrency, baseCurrency, date)
                    : 0m;
                var unrealizedBase = marketValueBase - costBase;

                positionsValueBase += marketValueBase;
                costBaseTotal += costBase;
                realizedBaseTotal += realizedBase;

                if (date >= persistFrom && (qty != 0 || marketValueBase != 0 || realizedBase != 0m))
                {
                    dailyPositionSnapshots.Add(new PositionSnapshot
                    {
                        Id = Guid.NewGuid(),
                        PortfolioId = portfolioId,
                        InstrumentId = instrumentId,
                        Date = date,
                        Quantity = qty,
                        CostBase = costBase,
                        MarketValueBase = marketValueBase,
                        UnrealizedBase = unrealizedBase,
                        RealizedBase = realizedBase
                    });
                }
            }

            var cashBase = 0m;
            foreach (var cash in cashByCurrency)
            {
                cashBase += data.Convert(cash.Value, cash.Key, baseCurrency, date);
            }

            var navBase = cashBase + positionsValueBase;
            var flowBase = netFlowByDate.TryGetValue(date.Date, out var flow) ? flow : 0m;
            var pnlDayBase = navBase - prevNav - flowBase;

            if (date >= persistFrom)
            {
                portfolioSnapshots.Add(new PortfolioSnapshot
                {
                    Id = Guid.NewGuid(),
                    PortfolioId = portfolioId,
                    Date = date,
                    NavBase = navBase,
                    PnlDayBase = pnlDayBase,
                    PnlMonthBase = 0,
                    PnlYearBase = 0,
                    CashBase = cashBase
                });

                positionSnapshots.AddRange(dailyPositionSnapshots);
            }

            prevNav = navBase;
        }

        if (portfolioSnapshots.Count > 0)
        {
            await context.PortfolioSnapshots.AddRangeAsync(portfolioSnapshots, cancellationToken);
        }

        if (positionSnapshots.Count > 0)
        {
            await context.PositionSnapshots.AddRangeAsync(positionSnapshots, cancellationToken);
        }

        await context.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Rebuilt snapshots for portfolio {PortfolioId} from {FromDate} to {ToDate} ({SnapshotCount} days)",
            portfolioId, persistFrom, calculationEnd, portfolioSnapshots.Count);
    }

    private static void ApplyCash(
        Operation op,
        IDictionary<string, decimal> cashByCurrency,
        bool usesBrokerCashLedger,
        DateTime asOfDate)
    {
        var amount = op.Price != 0 ? op.Price : op.Quantity;
        var tradeValue = op.Quantity * op.Price;
        var cashEffective = BrokerCashLedgerHelper.GetCashEffectiveDate(op) <= asOfDate.Date;

        switch (op.Type)
        {
            case OperationType.Buy when op.InstrumentId != null:
                var hasBuyCashLedger = BrokerCashLedgerHelper.IsImportedBrokerOperation(op, usesBrokerCashLedger);
                if (hasBuyCashLedger)
                {
                    break;
                }

                if (!cashEffective)
                {
                    break;
                }

                AddCash(op.CurrencyId, -(tradeValue + op.Fee), cashByCurrency);
                break;
            case OperationType.Sell when op.InstrumentId != null:
                var hasSellCashLedger = BrokerCashLedgerHelper.IsImportedBrokerOperation(op, usesBrokerCashLedger);
                if (hasSellCashLedger)
                {
                    break;
                }

                if (!cashEffective)
                {
                    break;
                }

                AddCash(op.CurrencyId, tradeValue - op.Fee, cashByCurrency);
                break;
            case OperationType.BondPartialRedemption when op.InstrumentId != null:
                if (!cashEffective)
                {
                    break;
                }

                AddCash(op.CurrencyId, tradeValue - op.Fee, cashByCurrency);
                break;
            case OperationType.BondMaturity when op.InstrumentId != null:
                if (!cashEffective)
                {
                    break;
                }

                AddCash(op.CurrencyId, tradeValue - op.Fee, cashByCurrency);
                break;
            case OperationType.Split when op.InstrumentId != null:
            case OperationType.ReverseSplit when op.InstrumentId != null:
                break;
            case OperationType.Dividend:
                AddCash(op.CurrencyId, amount, cashByCurrency);
                break;
            case OperationType.Fee:
                AddCash(op.CurrencyId, amount != 0 ? -amount : -op.Fee, cashByCurrency);
                break;
            case OperationType.CashAdjustment:
                if (BrokerCashLedgerHelper.AffectsCashBalance(op, usesBrokerCashLedger))
                {
                    AddCash(op.CurrencyId, op.Price, cashByCurrency);
                }
                break;
            case OperationType.Deposit:
                AddCash(op.CurrencyId, amount, cashByCurrency);
                break;
            case OperationType.Withdraw:
                AddCash(op.CurrencyId, -amount, cashByCurrency);
                break;
            case OperationType.TransferIn when op.InstrumentId == null:
                AddCash(op.CurrencyId, amount, cashByCurrency);
                break;
            case OperationType.TransferOut when op.InstrumentId == null:
                AddCash(op.CurrencyId, -amount, cashByCurrency);
                break;
        }
    }

    private static void AddCash(string currencyId, decimal amount, IDictionary<string, decimal> cashByCurrency)
    {
        if (cashByCurrency.TryGetValue(currencyId, out var existing))
        {
            cashByCurrency[currencyId] = existing + amount;
        }
        else
        {
            cashByCurrency[currencyId] = amount;
        }
    }

    private static void TrackFlows(Operation op, string baseCurrency, DateTime date, HistoricalDataLookup data,
        IDictionary<DateTime, decimal> flows)
    {
        var amount = op.Price != 0 ? op.Price : op.Quantity;
        decimal? flowBase = op.Type switch
        {
            OperationType.Deposit => data.Convert(amount, op.CurrencyId, baseCurrency, op.TradeDate),
            OperationType.Withdraw => -data.Convert(amount, op.CurrencyId, baseCurrency, op.TradeDate),
            OperationType.TransferIn when op.InstrumentId == null => data.Convert(amount, op.CurrencyId, baseCurrency, op.TradeDate),
            OperationType.TransferOut when op.InstrumentId == null => -data.Convert(amount, op.CurrencyId, baseCurrency, op.TradeDate),
            _ => null
        };

        if (flowBase is null) return;

        var key = date.Date;
        if (flows.TryGetValue(key, out var existing))
        {
            flows[key] = existing + flowBase.Value;
        }
        else
        {
            flows[key] = flowBase.Value;
        }
    }

    private async Task RemoveSnapshots(Guid portfolioId, DateTime fromDate, CancellationToken cancellationToken)
    {
        var portfolioSnaps = context.PortfolioSnapshots
            .Where(x => x.PortfolioId == portfolioId && x.Date >= fromDate);
        var positionSnaps = context.PositionSnapshots
            .Where(x => x.PortfolioId == portfolioId && x.Date >= fromDate);

        context.PortfolioSnapshots.RemoveRange(portfolioSnaps);
        context.PositionSnapshots.RemoveRange(positionSnaps);
        await context.SaveChangesAsync(cancellationToken);
    }
}
