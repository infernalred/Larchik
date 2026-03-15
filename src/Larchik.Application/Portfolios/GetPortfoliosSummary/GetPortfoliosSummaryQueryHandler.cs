using Larchik.Application.Contracts;
using Larchik.Application.Helpers;
using Larchik.Application.Models;
using Larchik.Application.Portfolios.Valuation;
using Larchik.Persistence.Context;
using Larchik.Persistence.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Larchik.Application.Portfolios.GetPortfoliosSummary;

public class GetPortfoliosSummaryQueryHandler(LarchikContext context, IUserAccessor userAccessor)
    : IRequestHandler<GetPortfoliosSummaryQuery, Result<PortfoliosSummaryDto>>
{
    public async Task<Result<PortfoliosSummaryDto>> Handle(
        GetPortfoliosSummaryQuery request,
        CancellationToken cancellationToken)
    {
        var userId = userAccessor.GetUserId();
        var portfolios = await context.Portfolios
            .Include(x => x.Broker)
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .ToListAsync(cancellationToken);

        if (portfolios.Count == 0)
        {
            return Result<PortfoliosSummaryDto>.Failure("No portfolios found");
        }

        var baseCurrency = ResolveBaseCurrency(request.Currency, portfolios);
        if (baseCurrency is null)
        {
            return Result<PortfoliosSummaryDto>.Failure(
                "Portfolios use different reporting currencies. Specify the 'currency' query parameter.");
        }

        var asOfDateTime = DateTime.UtcNow;
        var asOfDate = asOfDateTime.Date;
        var portfolioIds = portfolios.Select(x => x.Id).ToArray();

        var operations = await context.Operations
            .AsNoTracking()
            .Where(x => portfolioIds.Contains(x.PortfolioId) && x.TradeDate <= asOfDateTime)
            .OrderBy(x => x.PortfolioId)
            .ThenBy(x => x.TradeDate)
            .ThenBy(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

        var instrumentIds = operations
            .Where(x => x.InstrumentId != null)
            .Select(x => x.InstrumentId!.Value)
            .Distinct()
            .ToArray();

        var instruments = await context.Instruments
            .AsNoTracking()
            .Where(x => instrumentIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, cancellationToken);

        var prices = await context.Prices
            .AsNoTracking()
            .Where(x => instrumentIds.Contains(x.InstrumentId))
            .ToListAsync(cancellationToken);

        var neededCurrencies = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { baseCurrency };
        foreach (var op in operations)
        {
            neededCurrencies.Add(op.CurrencyId);
        }

        foreach (var instrument in instruments.Values)
        {
            neededCurrencies.Add(instrument.CurrencyId);
        }

        var fxRates = await context.FxRates
            .AsNoTracking()
            .Where(x => neededCurrencies.Contains(x.BaseCurrencyId) && neededCurrencies.Contains(x.QuoteCurrencyId))
            .ToListAsync(cancellationToken);

        var data = new HistoricalDataLookup(prices, fxRates);
        var operationsByPortfolio = operations
            .GroupBy(x => x.PortfolioId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<Operation>)g.ToList());

        var method = request.Method ?? "adjustingAvg";
        var valuationService = new ValuationService();

        decimal totalNetInflowBase = 0;
        decimal totalGrossDepositsBase = 0;
        decimal totalGrossWithdrawalsBase = 0;
        decimal totalCashBase = 0;
        decimal totalPositionsValueBase = 0;
        decimal totalRealizedBase = 0;
        decimal totalUnrealizedBase = 0;

        foreach (var portfolio in portfolios)
        {
            var portfolioOperations = operationsByPortfolio.GetValueOrDefault(portfolio.Id) ?? [];
            var (netInflowBase, grossDepositsBase, grossWithdrawalsBase, cashBase, positionsValueBase, realizedBase, unrealizedBase) =
                CalculatePortfolioMetrics(
                    portfolioOperations,
                    instruments,
                    data,
                    valuationService,
                    method,
                    baseCurrency,
                    asOfDate,
                    UsesBrokerCashLedger(portfolio));

            totalNetInflowBase += netInflowBase;
            totalGrossDepositsBase += grossDepositsBase;
            totalGrossWithdrawalsBase += grossWithdrawalsBase;
            totalCashBase += cashBase;
            totalPositionsValueBase += positionsValueBase;
            totalRealizedBase += realizedBase;
            totalUnrealizedBase += unrealizedBase;
        }

        var navBase = totalCashBase + totalPositionsValueBase;
        var pnlBase = totalRealizedBase + totalUnrealizedBase;

        return Result<PortfoliosSummaryDto>.Success(new PortfoliosSummaryDto
        {
            ReportingCurrencyId = baseCurrency,
            PortfolioCount = portfolios.Count,
            NetInflowBase = totalNetInflowBase,
            GrossDepositsBase = totalGrossDepositsBase,
            GrossWithdrawalsBase = totalGrossWithdrawalsBase,
            CashBase = totalCashBase,
            PositionsValueBase = totalPositionsValueBase,
            RealizedBase = totalRealizedBase,
            UnrealizedBase = totalUnrealizedBase,
            PnlBase = pnlBase,
            ValuationMethod = method,
            NavBase = navBase
        });
    }

    private static (decimal NetInflowBase, decimal GrossDepositsBase, decimal GrossWithdrawalsBase, decimal CashBase, decimal PositionsValueBase, decimal RealizedBase, decimal UnrealizedBase)
        CalculatePortfolioMetrics(
            IReadOnlyList<Operation> operations,
            IReadOnlyDictionary<Guid, Instrument> instruments,
            HistoricalDataLookup data,
        ValuationService valuationService,
        string valuationMethod,
        string baseCurrency,
        DateTime asOfDate,
        bool usesBrokerCashLedger)
    {
        var cashByCurrency = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        var positions = new Dictionary<Guid, decimal>();
        var valuationOperations = new List<ValuationOperation>();
        decimal netInflowBase = 0;
        decimal grossDepositsBase = 0;
        decimal grossWithdrawalsBase = 0;

        foreach (var op in operations)
        {
            var instrument = op.InstrumentId is not null && instruments.TryGetValue(op.InstrumentId.Value, out var resolvedInstrument)
                ? resolvedInstrument
                : null;
            var cashEffective = IsCashEffective(op, asOfDate);
            var amount = op.Price != 0 ? op.Price : op.Quantity;
            var tradeValue = op.Quantity * op.Price;
            switch (op.Type)
            {
                case OperationType.Buy when op.InstrumentId != null:
                    if (usesBrokerCashLedger)
                    {
                        if (instrument?.Type != InstrumentType.Currency)
                        {
                            AddPosition(op.InstrumentId.Value, op.Quantity, positions);
                        }

                        if (cashEffective && op.Fee != 0)
                        {
                            AddCash(op.CurrencyId, -op.Fee, cashByCurrency);
                        }

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

                    AddPosition(op.InstrumentId.Value, op.Quantity, positions);
                    if (cashEffective)
                    {
                        AddCash(op.CurrencyId, -(tradeValue + op.Fee), cashByCurrency);
                    }
                    break;
                case OperationType.Sell when op.InstrumentId != null:
                case OperationType.BondMaturity when op.InstrumentId != null:
                    if (usesBrokerCashLedger)
                    {
                        if (instrument?.Type != InstrumentType.Currency)
                        {
                            AddPosition(op.InstrumentId.Value, -op.Quantity, positions);
                        }

                        if (cashEffective && op.Fee != 0)
                        {
                            AddCash(op.CurrencyId, -op.Fee, cashByCurrency);
                        }

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

                    AddPosition(op.InstrumentId.Value, -op.Quantity, positions);
                    if (cashEffective)
                    {
                        AddCash(op.CurrencyId, tradeValue - op.Fee, cashByCurrency);
                    }
                    break;
                case OperationType.BondPartialRedemption when op.InstrumentId != null:
                    if (usesBrokerCashLedger)
                    {
                        if (cashEffective && op.Fee != 0)
                        {
                            AddCash(op.CurrencyId, -op.Fee, cashByCurrency);
                        }

                        break;
                    }

                    if (cashEffective)
                    {
                        AddCash(op.CurrencyId, tradeValue - op.Fee, cashByCurrency);
                    }
                    break;
                case OperationType.Split when op.InstrumentId != null:
                case OperationType.ReverseSplit when op.InstrumentId != null:
                    if (instrument?.Type == InstrumentType.Currency)
                    {
                        break;
                    }

                    ApplySplitFactor(op.InstrumentId.Value, op.Quantity, positions);
                    break;
                case OperationType.Dividend:
                    AddCash(op.CurrencyId, amount, cashByCurrency);
                    break;
                case OperationType.Fee:
                    AddCash(op.CurrencyId, amount != 0 ? -amount : -op.Fee, cashByCurrency);
                    break;
                case OperationType.CashAdjustment:
                    AddCash(op.CurrencyId, op.Price, cashByCurrency);
                    break;
                case OperationType.Deposit:
                    AddCash(op.CurrencyId, amount, cashByCurrency);
                    var depositBase = data.Convert(amount, op.CurrencyId, baseCurrency, op.TradeDate);
                    netInflowBase += depositBase;
                    grossDepositsBase += depositBase;
                    break;
                case OperationType.Withdraw:
                    AddCash(op.CurrencyId, -amount, cashByCurrency);
                    var withdrawBase = data.Convert(amount, op.CurrencyId, baseCurrency, op.TradeDate);
                    netInflowBase -= withdrawBase;
                    grossWithdrawalsBase += withdrawBase;
                    break;
                case OperationType.TransferIn:
                    if (op.InstrumentId != null)
                    {
                        if (instrument?.Type == InstrumentType.Currency)
                        {
                            AddCash(instrument.CurrencyId, op.Quantity, cashByCurrency);
                            break;
                        }

                        AddPosition(op.InstrumentId.Value, op.Quantity, positions);
                    }
                    else
                    {
                        AddCash(op.CurrencyId, amount, cashByCurrency);
                        var transferInBase = data.Convert(amount, op.CurrencyId, baseCurrency, op.TradeDate);
                        netInflowBase += transferInBase;
                        grossDepositsBase += transferInBase;
                    }

                    break;
                case OperationType.TransferOut:
                    if (op.InstrumentId != null)
                    {
                        if (instrument?.Type == InstrumentType.Currency)
                        {
                            AddCash(instrument.CurrencyId, -op.Quantity, cashByCurrency);
                            break;
                        }

                        AddPosition(op.InstrumentId.Value, -op.Quantity, positions);
                    }
                    else
                    {
                        AddCash(op.CurrencyId, -amount, cashByCurrency);
                        var transferOutBase = data.Convert(amount, op.CurrencyId, baseCurrency, op.TradeDate);
                        netInflowBase -= transferOutBase;
                        grossWithdrawalsBase += transferOutBase;
                    }

                    break;
            }

            if (op.InstrumentId is null) continue;
            if (instrument?.Type == InstrumentType.Currency) continue;

            var instrumentCurrency = instrument?.CurrencyId ?? op.CurrencyId;
            var priceInInstrument = data.Convert(op.Price, op.CurrencyId, instrumentCurrency, op.TradeDate);
            var feeInInstrument = data.Convert(op.Fee, op.CurrencyId, instrumentCurrency, op.TradeDate);

            valuationOperations.Add(new ValuationOperation(
                op.InstrumentId.Value,
                op.Type,
                op.Quantity,
                priceInInstrument,
                feeInInstrument,
                op.TradeDate,
                op.CreatedAt));
        }

        var valuation = valuationService.Evaluate(valuationOperations, valuationMethod, assumeSorted: true);
        var positionCosts = valuation.Positions;

        var cashBase = 0m;
        foreach (var kvp in cashByCurrency)
        {
            cashBase += data.Convert(kvp.Value, kvp.Key, baseCurrency, asOfDate);
        }

        var positionsValueBase = 0m;
        var costBasisBase = 0m;
        foreach (var kvp in positions)
        {
            if (kvp.Value == 0) continue;
            if (!instruments.TryGetValue(kvp.Key, out var instrument)) continue;

            positionCosts.TryGetValue(kvp.Key, out var cost);
            var price = data.GetPrice(kvp.Key, asOfDate);
            var lastPrice = price?.Value;
            var marketValueBase = lastPrice.HasValue
                ? data.Convert(kvp.Value * lastPrice.Value, price!.CurrencyId, baseCurrency, asOfDate)
                : 0;
            var avgCost = cost?.AverageCost ?? 0;
            var costBase = data.Convert(avgCost * kvp.Value, instrument.CurrencyId, baseCurrency, asOfDate);

            positionsValueBase += marketValueBase;
            costBasisBase += costBase;
        }

        var realizedBase = 0m;
        foreach (var kvp in valuation.RealizedByInstrument)
        {
            if (instruments.TryGetValue(kvp.Key, out var instrument))
            {
                realizedBase += data.Convert(kvp.Value, instrument.CurrencyId, baseCurrency, asOfDate);
            }
            else
            {
                realizedBase += kvp.Value;
            }
        }

        return (
            netInflowBase,
            grossDepositsBase,
            grossWithdrawalsBase,
            cashBase,
            positionsValueBase,
            realizedBase,
            positionsValueBase - costBasisBase);
    }

    private static string? ResolveBaseCurrency(string? requestedCurrency, IReadOnlyCollection<Portfolio> portfolios)
    {
        if (!string.IsNullOrWhiteSpace(requestedCurrency))
        {
            return requestedCurrency.Trim().ToUpperInvariant();
        }

        var distinct = portfolios
            .Select(x => x.ReportingCurrencyId.ToUpperInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return distinct.Length == 1 ? distinct[0] : null;
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

    private static void AddPosition(Guid instrumentId, decimal quantity, IDictionary<Guid, decimal> positions)
    {
        if (positions.TryGetValue(instrumentId, out var existing))
        {
            positions[instrumentId] = existing + quantity;
        }
        else
        {
            positions[instrumentId] = quantity;
        }
    }

    private static void ApplySplitFactor(Guid instrumentId, decimal factor, IDictionary<Guid, decimal> positions)
    {
        if (factor <= 0) return;
        if (!positions.TryGetValue(instrumentId, out var existing)) return;
        var updated = existing * factor;
        positions[instrumentId] = factor < 1m
            ? decimal.Round(updated, 0, MidpointRounding.AwayFromZero)
            : updated;
    }

    private static bool IsCashEffective(Operation operation, DateTime asOfDate)
    {
        return GetCashEffectiveDate(operation) <= asOfDate;
    }

    private static DateTime GetCashEffectiveDate(Operation operation)
    {
        return operation.InstrumentId is null
            ? operation.TradeDate.Date
            : (operation.SettlementDate ?? operation.TradeDate).Date;
    }

    private static bool UsesBrokerCashLedger(Portfolio portfolio)
    {
        return string.Equals(portfolio.Broker?.Code, "tbank", StringComparison.OrdinalIgnoreCase);
    }
}
