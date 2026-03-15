using Larchik.Application.Contracts;
using Larchik.Application.Helpers;
using Larchik.Application.Models;
using Larchik.Application.Portfolios.Valuation;
using Larchik.Persistence.Context;
using Larchik.Persistence.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Larchik.Application.Portfolios.GetPortfolioSummary;

public class GetPortfolioSummaryQueryHandler(LarchikContext context, IUserAccessor userAccessor)
    : IRequestHandler<GetPortfolioSummaryQuery, Result<PortfolioSummaryDto>?>
{
    public async Task<Result<PortfolioSummaryDto>?> Handle(GetPortfolioSummaryQuery request, CancellationToken cancellationToken)
    {
        var userId = userAccessor.GetUserId();
        var portfolio = await context.Portfolios
            .Include(x => x.Broker)
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == request.Id && x.UserId == userId, cancellationToken);

        if (portfolio is null) return null;

        var asOfDateTime = DateTime.UtcNow;
        var asOfDate = asOfDateTime.Date;

        var operations = await context.Operations
            .AsNoTracking()
            .Where(x => x.PortfolioId == request.Id && x.TradeDate <= asOfDateTime)
            .OrderBy(x => x.TradeDate)
            .ThenBy(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

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

        var neededCurrencies = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            portfolio.ReportingCurrencyId
        };

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
        var usesBrokerCashLedger = UsesBrokerCashLedger(portfolio);

        var cashByCurrency = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        var positions = new Dictionary<Guid, decimal>();
        decimal netInflowBase = 0;

        var valuationOperations = new List<ValuationOperation>();
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
                case OperationType.BondMaturity when op.InstrumentId != null:
                    if (usesBrokerCashLedger)
                    {
                        AddPosition(op.InstrumentId.Value, -op.Quantity, positions);
                        if (cashEffective && op.Fee != 0)
                        {
                            AddCash(op.CurrencyId, -op.Fee, cashByCurrency);
                        }
                        break;
                    }

                    AddPosition(op.InstrumentId.Value, -op.Quantity, positions);
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
                    netInflowBase += data.Convert(amount, op.CurrencyId, portfolio.ReportingCurrencyId, op.TradeDate);
                    break;
                case OperationType.Withdraw:
                    AddCash(op.CurrencyId, -amount, cashByCurrency);
                    netInflowBase -= data.Convert(amount, op.CurrencyId, portfolio.ReportingCurrencyId, op.TradeDate);
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
                        netInflowBase += data.Convert(amount, op.CurrencyId, portfolio.ReportingCurrencyId, op.TradeDate);
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
                        netInflowBase -= data.Convert(amount, op.CurrencyId, portfolio.ReportingCurrencyId, op.TradeDate);
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

        var valuationService = new ValuationService();
        var valuation = valuationService.Evaluate(valuationOperations, request.Method, assumeSorted: true);
        var positionCosts = valuation.Positions;

        var cashDtos = new List<CashBalanceDto>();
        decimal cashBase = 0;
        foreach (var kvp in cashByCurrency)
        {
            var amountBase = data.Convert(kvp.Value, kvp.Key, portfolio.ReportingCurrencyId, asOfDate);
            cashDtos.Add(new CashBalanceDto
            {
                CurrencyId = kvp.Key.ToUpperInvariant(),
                Amount = kvp.Value,
                AmountInBase = amountBase
            });
            cashBase += amountBase;
        }

        var positionDtos = new List<PositionHoldingDto>();
        decimal positionsValueBase = 0;
        decimal costBasisBase = 0;
        foreach (var kvp in positions)
        {
            if (kvp.Value == 0) continue;
            if (!instruments.TryGetValue(kvp.Key, out var instrument)) continue;

            positionCosts.TryGetValue(kvp.Key, out var cost);
            var price = data.GetPrice(kvp.Key, asOfDate);
            var lastPrice = price?.Value;
            var marketValueBase = lastPrice.HasValue
                ? data.Convert(kvp.Value * lastPrice.Value, price!.CurrencyId, portfolio.ReportingCurrencyId, asOfDate)
                : 0;
            var avgCost = cost?.AverageCost ?? 0;
            var costBase = data.Convert(avgCost * kvp.Value, instrument.CurrencyId, portfolio.ReportingCurrencyId, asOfDate);

            positionDtos.Add(new PositionHoldingDto
            {
                InstrumentId = kvp.Key,
                InstrumentName = instrument.Name,
                InstrumentType = instrument.Type.ToString(),
                CurrencyId = instrument.CurrencyId,
                Quantity = kvp.Value,
                LastPrice = lastPrice,
                MarketValueBase = marketValueBase,
                AverageCost = avgCost
            });

            positionsValueBase += marketValueBase;
            costBasisBase += costBase;
        }

        var realizedBase = 0m;
        foreach (var kvp in valuation.RealizedByInstrument)
        {
            if (instruments.TryGetValue(kvp.Key, out var instrument))
            {
                realizedBase += data.Convert(kvp.Value, instrument.CurrencyId, portfolio.ReportingCurrencyId, asOfDate);
            }
            else
            {
                realizedBase += kvp.Value;
            }
        }

        var realizedDtos = new List<RealizedPnlDto>();
        foreach (var kvp in valuation.RealizedByInstrument)
        {
            var instrumentName = instruments.TryGetValue(kvp.Key, out var instrument)
                ? instrument.Name
                : kvp.Key.ToString();
            var instrumentCurrency = instruments.TryGetValue(kvp.Key, out var inst2)
                ? inst2.CurrencyId
                : portfolio.ReportingCurrencyId;
            var realizedBaseValue = data.Convert(kvp.Value, instrumentCurrency, portfolio.ReportingCurrencyId, asOfDate);

            realizedDtos.Add(new RealizedPnlDto
            {
                InstrumentId = kvp.Key,
                InstrumentName = instrumentName,
                CurrencyId = instrumentCurrency,
                Realized = kvp.Value,
                RealizedBase = realizedBaseValue
            });
        }

        var summary = new PortfolioSummaryDto
        {
            Id = portfolio.Id,
            Name = portfolio.Name,
            ReportingCurrencyId = portfolio.ReportingCurrencyId,
            NetInflowBase = netInflowBase,
            CashBase = cashBase,
            PositionsValueBase = positionsValueBase,
            RealizedBase = realizedBase,
            UnrealizedBase = positionsValueBase - costBasisBase,
            NavBase = cashBase + positionsValueBase,
            ValuationMethod = request.Method ?? "adjustingAvg",
            Cash = cashDtos,
            Positions = positionDtos,
            RealizedByInstrument = realizedDtos
        };

        return Result<PortfolioSummaryDto>.Success(summary);
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
