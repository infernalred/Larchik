using Larchik.Application.Contracts;
using Larchik.Application.Helpers;
using Larchik.Application.Models;
using Larchik.Application.Portfolios.Valuation;
using Larchik.Persistence.Context;
using Larchik.Persistence.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Larchik.Application.Portfolios.GetPortfolioPerformance;

public class GetPortfolioPerformanceQueryHandler(LarchikContext context, IUserAccessor userAccessor)
    : IRequestHandler<GetPortfolioPerformanceQuery, Result<IReadOnlyCollection<PortfolioPerformanceDto>>?>
{
    public async Task<Result<IReadOnlyCollection<PortfolioPerformanceDto>>?> Handle(
        GetPortfolioPerformanceQuery request,
        CancellationToken cancellationToken)
    {
        var userId = userAccessor.GetUserId();
        var portfolio = await context.Portfolios
            .Include(x => x.Broker)
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == request.Id && x.UserId == userId, cancellationToken);

        if (portfolio is null) return null;

        var baseCurrency = portfolio.ReportingCurrencyId;
        var operations = await context.Operations
            .AsNoTracking()
            .Where(x => x.PortfolioId == request.Id)
            .OrderBy(x => x.TradeDate)
            .ThenBy(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

        if (operations.Count == 0)
        {
            return Result<IReadOnlyCollection<PortfolioPerformanceDto>>.Success([]);
        }

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

        var method = request.Method ?? "adjustingAvg";
        var fromDate = request.From?.Date ?? operations.First().TradeDate.Date;
        var toDate = request.To?.Date ?? DateTime.UtcNow.Date;
        var cursor = new DateTime(fromDate.Year, fromDate.Month, 1);
        var lastMonthEnd = new DateTime(toDate.Year, toDate.Month, DateTime.DaysInMonth(toDate.Year, toDate.Month));

        var accountingCurrencies = InstrumentAccountingCurrencyHelper.Build(operations, instruments, baseCurrency);
        var valuationOperations = BuildValuationOperations(operations, instruments, data, baseCurrency);
        var valuationService = new ValuationService();
        var usesBrokerCashLedger = BrokerCashLedgerHelper.UsesBrokerCashLedger(portfolio);

        var results = new List<PortfolioPerformanceDto>();

        while (cursor <= lastMonthEnd)
        {
            var monthEnd = new DateTime(cursor.Year, cursor.Month, DateTime.DaysInMonth(cursor.Year, cursor.Month));
            if (monthEnd > lastMonthEnd) monthEnd = lastMonthEnd;
            var startBoundary = cursor.AddDays(-1);

            var startSnapshot = ComputeValuation(startBoundary, method, baseCurrency, data, instruments, operations,
                valuationOperations, valuationService, usesBrokerCashLedger, accountingCurrencies);
            var endSnapshot = ComputeValuation(monthEnd, method, baseCurrency, data, instruments, operations,
                valuationOperations, valuationService, usesBrokerCashLedger, accountingCurrencies);

            var netFlow = ComputeFlows(operations, data, baseCurrency, cursor, monthEnd);

            // If we have no data up to the month end, skip emitting an empty period.
            if (endSnapshot.NavBase == 0 && startSnapshot.NavBase == 0 && netFlow == 0)
            {
                cursor = cursor.AddMonths(1);
                continue;
            }

            var pnl = endSnapshot.NavBase - startSnapshot.NavBase - netFlow;
            var returnPct = startSnapshot.NavBase != 0 ? pnl / startSnapshot.NavBase : 0m;
            var realizedDelta = endSnapshot.RealizedBase - startSnapshot.RealizedBase;

            results.Add(new PortfolioPerformanceDto
            {
                Period = $"{cursor:yyyy-MM}",
                StartDate = cursor,
                EndDate = monthEnd,
                ReportingCurrencyId = baseCurrency,
                ValuationMethod = method,
                StartNavBase = startSnapshot.NavBase,
                EndNavBase = endSnapshot.NavBase,
                NetInflowBase = netFlow,
                PnlBase = pnl,
                ReturnPct = returnPct,
                RealizedBase = realizedDelta,
                UnrealizedBase = endSnapshot.UnrealizedBase
            });

            cursor = cursor.AddMonths(1);
        }

        return Result<IReadOnlyCollection<PortfolioPerformanceDto>>.Success(results);
    }

    private static IReadOnlyList<ValuationOperation> BuildValuationOperations(
        IEnumerable<Operation> operations,
        IReadOnlyDictionary<Guid, Instrument> instruments,
        HistoricalDataLookup data,
        string baseCurrency)
    {
        var accountingCurrencies = InstrumentAccountingCurrencyHelper.Build(operations, instruments, baseCurrency);
        var valuationOperations = new List<ValuationOperation>();

        foreach (var op in operations)
        {
            if (op.InstrumentId is null) continue;
            if (instruments.TryGetValue(op.InstrumentId.Value, out var instrument) && instrument.Type == InstrumentType.Currency)
            {
                continue;
            }

            var accountingCurrency = InstrumentAccountingCurrencyHelper.Get(op.InstrumentId.Value, accountingCurrencies, instruments, baseCurrency);
            var price = data.Convert(op.Price, op.CurrencyId, accountingCurrency, op.TradeDate);
            var fee = data.Convert(op.Fee, op.CurrencyId, accountingCurrency, op.TradeDate);

            valuationOperations.Add(new ValuationOperation(
                op.InstrumentId.Value,
                op.Type,
                op.Quantity,
                price,
                fee,
                op.TradeDate,
                op.CreatedAt));
        }

        return valuationOperations;
    }

    private static ValuationSnapshot ComputeValuation(
        DateTime asOfDate,
        string method,
        string baseCurrency,
        HistoricalDataLookup data,
        IReadOnlyDictionary<Guid, Instrument> instruments,
        IReadOnlyList<Operation> operations,
        IReadOnlyList<ValuationOperation> valuationOperations,
        ValuationService valuationService,
        bool usesBrokerCashLedger,
        IReadOnlyDictionary<Guid, string> accountingCurrencies)
    {
        var cashByCurrency = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);

        foreach (var op in operations)
        {
            if (op.TradeDate.Date > asOfDate.Date) break;

            var instrument = op.InstrumentId is not null && instruments.TryGetValue(op.InstrumentId.Value, out var resolvedInstrument)
                ? resolvedInstrument
                : null;
            var cashEffective = BrokerCashLedgerHelper.IsCashEffective(op, asOfDate);
            var amount = op.Price != 0 ? op.Price : op.Quantity;
            var tradeValue = op.Quantity * op.Price;

            switch (op.Type)
            {
                case OperationType.Buy when op.InstrumentId != null:
                    var hasBuyCashLedger = BrokerCashLedgerHelper.IsImportedBrokerOperation(op, usesBrokerCashLedger);
                    if (hasBuyCashLedger)
                    {
                        break;
                    }

                    if (instrument?.Type == InstrumentType.Currency)
                    {
                        if (cashEffective)
                        {
                            AddCash(instrument.CurrencyId, op.Quantity, cashByCurrency);
                            AddCash(op.CurrencyId, -(tradeValue + op.Fee), cashByCurrency);
                        }
                        break;
                    }

                    if (cashEffective)
                    {
                        AddCash(op.CurrencyId, -(tradeValue + op.Fee), cashByCurrency);
                    }
                    break;
                case OperationType.Sell when op.InstrumentId != null:
                    var hasSellCashLedger = BrokerCashLedgerHelper.IsImportedBrokerOperation(op, usesBrokerCashLedger);
                    if (hasSellCashLedger)
                    {
                        break;
                    }

                    if (instrument?.Type == InstrumentType.Currency)
                    {
                        if (cashEffective)
                        {
                            AddCash(instrument.CurrencyId, -op.Quantity, cashByCurrency);
                            AddCash(op.CurrencyId, tradeValue - op.Fee, cashByCurrency);
                        }
                        break;
                    }

                    if (cashEffective)
                    {
                        AddCash(op.CurrencyId, tradeValue - op.Fee, cashByCurrency);
                    }
                    break;
                case OperationType.BondMaturity when op.InstrumentId != null:
                    if (cashEffective)
                    {
                        AddCash(op.CurrencyId, tradeValue - op.Fee, cashByCurrency);
                    }
                    break;
                case OperationType.BondPartialRedemption when op.InstrumentId != null:
                    if (cashEffective)
                    {
                        AddCash(op.CurrencyId, tradeValue - op.Fee, cashByCurrency);
                    }
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
                case OperationType.TransferIn when op.InstrumentId != null && instrument?.Type == InstrumentType.Currency:
                    AddCash(instrument.CurrencyId, op.Quantity, cashByCurrency);
                    break;
                case OperationType.TransferOut when op.InstrumentId != null && instrument?.Type == InstrumentType.Currency:
                    AddCash(instrument.CurrencyId, -op.Quantity, cashByCurrency);
                    break;
            }
        }

        var valuation = valuationService.Evaluate(
            valuationOperations.Where(x => x.TradeDate.Date <= asOfDate.Date),
            method,
            assumeSorted: true);

        var cashBase = 0m;
        foreach (var kvp in cashByCurrency)
        {
            cashBase += data.Convert(kvp.Value, kvp.Key, baseCurrency, asOfDate);
        }

        var positionsValueBase = 0m;
        var unrealizedBase = 0m;
        var realizedBase = 0m;

        foreach (var (instrumentId, position) in valuation.Positions)
        {
            var qty = position.Quantity;
            var instrumentCurrency = InstrumentAccountingCurrencyHelper.Get(instrumentId, accountingCurrencies, instruments, baseCurrency);
            var instrument = instruments.TryGetValue(instrumentId, out var resolvedInstrument)
                ? resolvedInstrument
                : null;
            if (instrument?.Type == InstrumentType.Currency)
            {
                continue;
            }
            var pricePoint = data.GetPrice(instrumentId, asOfDate);
            var priceValue = pricePoint?.Value;
            var quoteCurrency = pricePoint?.CurrencyId ?? instrumentCurrency;
            var marketValueBase = priceValue.HasValue
                ? data.Convert(qty * priceValue.Value, quoteCurrency, baseCurrency, asOfDate)
                : 0m;
            var avgCost = position.AverageCost;
            var costBase = data.Convert(avgCost * qty, instrumentCurrency, baseCurrency, asOfDate);
            var unrealized = marketValueBase - costBase;

            positionsValueBase += marketValueBase;
            unrealizedBase += unrealized;
        }

        foreach (var kvp in valuation.RealizedByInstrument)
        {
            var instrumentCurrency = InstrumentAccountingCurrencyHelper.Get(kvp.Key, accountingCurrencies, instruments, baseCurrency);
            realizedBase += data.Convert(kvp.Value, instrumentCurrency, baseCurrency, asOfDate);
        }

        return new ValuationSnapshot
        {
            NavBase = cashBase + positionsValueBase,
            CashBase = cashBase,
            PositionsValueBase = positionsValueBase,
            UnrealizedBase = unrealizedBase,
            RealizedBase = realizedBase
        };
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
            if (date < fromInclusive.Date || date > toInclusive.Date) continue;

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

    private record ValuationSnapshot
    {
        public decimal NavBase { get; init; }
        public decimal CashBase { get; init; }
        public decimal PositionsValueBase { get; init; }
        public decimal UnrealizedBase { get; init; }
        public decimal RealizedBase { get; init; }
    }
}
