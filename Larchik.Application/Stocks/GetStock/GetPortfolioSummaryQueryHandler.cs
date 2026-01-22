using Larchik.Application.Helpers;
using Larchik.Application.Models;
using Larchik.Application.Valuations;
using Larchik.Persistence.Context;
using Larchik.Persistence.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Larchik.Application.Portfolios.GetPortfolioSummary;

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

        var valuationStrategy = new AdjustingAverageValuationStrategy();
        var positionCosts = valuationStrategy.Compute(operations);

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
        foreach (var kvp in positions)
        {
            if (kvp.Value == 0) continue;
            if (!instruments.TryGetValue(kvp.Key, out var instrument)) continue;

            positionCosts.TryGetValue(kvp.Key, out var cost);
            var lastPrice = priceByInstrument.TryGetValue(kvp.Key, out var price) ? price.Value : (decimal?)null;
            var marketValueBase = lastPrice.HasValue
                ? ConvertToBase(kvp.Value * lastPrice.Value, instrument.CurrencyId, portfolio.ReportingCurrencyId, fxMap)
                : 0;

            positionDtos.Add(new PositionHoldingDto
            {
                InstrumentId = kvp.Key,
                InstrumentName = instrument.Name,
                CurrencyId = instrument.CurrencyId,
                Quantity = kvp.Value,
                LastPrice = lastPrice,
                MarketValueBase = marketValueBase,
                AverageCost = cost?.AverageCost ?? 0
            });

            positionsValueBase += marketValueBase;
        }

        var summary = new PortfolioSummaryDto
        {
            Id = portfolio.Id,
            Name = portfolio.Name,
            ReportingCurrencyId = portfolio.ReportingCurrencyId,
            NetInflowBase = netInflowBase,
            CashBase = cashBase,
            PositionsValueBase = positionsValueBase,
            NavBase = cashBase + positionsValueBase,
            Cash = cashDtos,
            Positions = positionDtos
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
}
