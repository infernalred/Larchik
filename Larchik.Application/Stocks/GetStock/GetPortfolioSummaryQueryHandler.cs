using Larchik.Application.Helpers;
using Larchik.Application.Models;
using Larchik.Persistence.Context;
using Larchik.Persistence.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Larchik.Application.Stocks.GetStock;

public class GetPortfolioSummaryQueryHandler(LarchikContext context)
    : IRequestHandler<GetPortfolioSummaryQuery, Result<PortfolioSummaryDto>?>
{
    public async Task<Result<PortfolioSummaryDto>?> Handle(GetPortfolioSummaryQuery request, CancellationToken cancellationToken)
    {
        var portfolio = await context.Portfolios
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);

        if (portfolio is null) return null;

        var operations = await context.Operations
            .AsNoTracking()
            .Where(x => x.PortfolioId == request.Id)
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

        var latestPrices = await context.Prices
            .AsNoTracking()
            .Where(x => instrumentIds.Contains(x.InstrumentId))
            .GroupBy(x => x.InstrumentId)
            .Select(g => g.OrderByDescending(p => p.Date).First())
            .ToListAsync(cancellationToken);

        var priceByInstrument = latestPrices.ToDictionary(x => x.InstrumentId);

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
            .GroupBy(x => new { x.BaseCurrencyId, x.QuoteCurrencyId })
            .Select(g => g.OrderByDescending(r => r.Date).First())
            .ToListAsync(cancellationToken);

        var fxMap = fxRates.ToDictionary(
            x => (x.BaseCurrencyId.ToUpperInvariant(), x.QuoteCurrencyId.ToUpperInvariant()),
            x => x.Rate);

        var cashByCurrency = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        var positions = new Dictionary<Guid, decimal>();
        decimal netInflowBase = 0;

        foreach (var op in operations)
        {
            var amount = op.Price != 0 ? op.Price : op.Quantity;
            var tradeValue = op.Quantity * op.Price;
            switch (op.Type)
            {
                case OperationType.Buy when op.InstrumentId != null:
                    AddPosition(op.InstrumentId.Value, op.Quantity, positions);
                    AddCash(op.CurrencyId, -(tradeValue + op.Fee), cashByCurrency);
                    break;
                case OperationType.Sell when op.InstrumentId != null:
                    AddPosition(op.InstrumentId.Value, -op.Quantity, positions);
                    AddCash(op.CurrencyId, tradeValue - op.Fee, cashByCurrency);
                    break;
                case OperationType.Dividend:
                    AddCash(op.CurrencyId, amount, cashByCurrency);
                    break;
                case OperationType.Fee:
                    AddCash(op.CurrencyId, amount != 0 ? -amount : -op.Fee, cashByCurrency);
                    break;
                case OperationType.Deposit:
                    AddCash(op.CurrencyId, amount, cashByCurrency);
                    netInflowBase += ConvertToBase(amount, op.CurrencyId, portfolio.ReportingCurrencyId, fxMap);
                    break;
                case OperationType.Withdraw:
                    AddCash(op.CurrencyId, -amount, cashByCurrency);
                    netInflowBase -= ConvertToBase(amount, op.CurrencyId, portfolio.ReportingCurrencyId, fxMap);
                    break;
                case OperationType.TransferIn:
                    if (op.InstrumentId != null)
                    {
                        AddPosition(op.InstrumentId.Value, op.Quantity, positions);
                    }
                    else
                    {
                        AddCash(op.CurrencyId, amount, cashByCurrency);
                        netInflowBase += ConvertToBase(amount, op.CurrencyId, portfolio.ReportingCurrencyId, fxMap);
                    }
                    break;
                case OperationType.TransferOut:
                    if (op.InstrumentId != null)
                    {
                        AddPosition(op.InstrumentId.Value, -op.Quantity, positions);
                    }
                    else
                    {
                        AddCash(op.CurrencyId, -amount, cashByCurrency);
                        netInflowBase -= ConvertToBase(amount, op.CurrencyId, portfolio.ReportingCurrencyId, fxMap);
                    }
                    break;
            }
        }

        var valuationOperations = new List<ValuationOperation>();
        foreach (var op in operations)
        {
            if (op.InstrumentId is null) continue;
            var instrument = instruments.GetValueOrDefault(op.InstrumentId.Value);
            var instrumentCurrency = instrument?.CurrencyId ?? op.CurrencyId;
            var priceInInstrument = ConvertCurrency(op.Price, op.CurrencyId, instrumentCurrency, fxMap);
            var feeInInstrument = ConvertCurrency(op.Fee, op.CurrencyId, instrumentCurrency, fxMap);

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
        var valuation = valuationService.Evaluate(valuationOperations, request.Method);
        var positionCosts = valuation.Positions;

        var cashDtos = new List<CashBalanceDto>();
        decimal cashBase = 0;
        foreach (var kvp in cashByCurrency)
        {
            var amountBase = ConvertToBase(kvp.Value, kvp.Key, portfolio.ReportingCurrencyId, fxMap);
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
            var lastPrice = priceByInstrument.TryGetValue(kvp.Key, out var price) ? price.Value : (decimal?)null;
            var marketValueBase = lastPrice.HasValue
                ? ConvertToBase(kvp.Value * lastPrice.Value, instrument.CurrencyId, portfolio.ReportingCurrencyId, fxMap)
                : 0;
            var avgCost = cost?.AverageCost ?? 0;
            var costBase = ConvertToBase(avgCost * kvp.Value, instrument.CurrencyId, portfolio.ReportingCurrencyId, fxMap);

            positionDtos.Add(new PositionHoldingDto
            {
                InstrumentId = kvp.Key,
                InstrumentName = instrument.Name,
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
                realizedBase += ConvertToBase(kvp.Value, instrument.CurrencyId, portfolio.ReportingCurrencyId, fxMap);
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
            var realizedBaseValue = ConvertToBase(kvp.Value, instrumentCurrency, portfolio.ReportingCurrencyId, fxMap);

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

    private static decimal ConvertToBase(decimal amount, string fromCurrency, string baseCurrency,
        IReadOnlyDictionary<(string Base, string Quote), decimal> fx)
    {
        if (string.Equals(fromCurrency, baseCurrency, StringComparison.OrdinalIgnoreCase)) return amount;

        var key = (baseCurrency.ToUpperInvariant(), fromCurrency.ToUpperInvariant());
        if (fx.TryGetValue(key, out var rate) && rate != 0)
        {
            return amount * rate;
        }

        var inverseKey = (fromCurrency.ToUpperInvariant(), baseCurrency.ToUpperInvariant());
        if (fx.TryGetValue(inverseKey, out var inverseRate) && inverseRate != 0)
        {
            return amount / inverseRate;
        }

        return amount;
    }

    private static decimal ConvertCurrency(decimal amount, string fromCurrency, string toCurrency,
        IReadOnlyDictionary<(string Base, string Quote), decimal> fx)
    {
        if (string.Equals(fromCurrency, toCurrency, StringComparison.OrdinalIgnoreCase)) return amount;
        var key = (toCurrency.ToUpperInvariant(), fromCurrency.ToUpperInvariant());
        if (fx.TryGetValue(key, out var rate) && rate != 0)
        {
            return amount * rate;
        }

        var inverseKey = (fromCurrency.ToUpperInvariant(), toCurrency.ToUpperInvariant());
        if (fx.TryGetValue(inverseKey, out var inverseRate) && inverseRate != 0)
        {
            return amount / inverseRate;
        }

        return amount;
    }
}
