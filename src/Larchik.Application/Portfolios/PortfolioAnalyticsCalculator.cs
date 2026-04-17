using Larchik.Application.Models;
using Larchik.Application.Portfolios.Valuation;
using Larchik.Persistence.Entities;

namespace Larchik.Application.Portfolios;

internal sealed class PortfolioAnalyticsCalculator
{
    public PortfolioSummaryDto CalculateSummary(
        Portfolio portfolio,
        IReadOnlyList<Operation> operations,
        IReadOnlyDictionary<Guid, Instrument> instruments,
        HistoricalDataLookup data,
        string valuationMethod,
        string baseCurrency,
        DateTime asOfDate)
    {
        var cashByCurrency = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        var positions = new Dictionary<Guid, decimal>();
        decimal netInflowBase = 0;
        decimal grossDepositsBase = 0;
        decimal grossWithdrawalsBase = 0;
        var valuationOperations = new List<ValuationOperation>();
        var usesBrokerCashLedger = BrokerCashLedgerHelper.UsesBrokerCashLedger(portfolio);
        var accountingCurrencies = InstrumentAccountingCurrencyHelper.Build(operations, instruments, baseCurrency);

        foreach (var op in operations)
        {
            if (op.TradeDate.Date > asOfDate.Date)
            {
                break;
            }

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
                        if (instrument?.Type != InstrumentType.Currency)
                        {
                            AddPosition(op.InstrumentId.Value, op.Quantity, positions);
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
                    var hasSellCashLedger = BrokerCashLedgerHelper.IsImportedBrokerOperation(op, usesBrokerCashLedger);
                    if (hasSellCashLedger)
                    {
                        if (instrument?.Type != InstrumentType.Currency)
                        {
                            AddPosition(op.InstrumentId.Value, -op.Quantity, positions);
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
                    if (cashEffective)
                    {
                        AddCash(op.CurrencyId, tradeValue - op.Fee, cashByCurrency);
                    }

                    break;
                case OperationType.BondMaturity when op.InstrumentId != null:
                    AddPosition(op.InstrumentId.Value, -op.Quantity, positions);
                    if (cashEffective)
                    {
                        AddCash(op.CurrencyId, tradeValue - op.Fee, cashByCurrency);
                    }

                    break;
                case OperationType.Split when op.InstrumentId != null:
                case OperationType.ReverseSplit when op.InstrumentId != null:
                    if (instrument?.Type != InstrumentType.Currency)
                    {
                        ApplySplitFactor(op.InstrumentId.Value, op.Quantity, positions);
                    }

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

            if (op.InstrumentId is null || instrument?.Type == InstrumentType.Currency)
            {
                continue;
            }

            var accountingCurrency = InstrumentAccountingCurrencyHelper.Get(op.InstrumentId.Value, accountingCurrencies, instruments, baseCurrency);
            var priceInAccounting = data.Convert(op.Price, op.CurrencyId, accountingCurrency, op.TradeDate);
            var feeInAccounting = data.Convert(op.Fee, op.CurrencyId, accountingCurrency, op.TradeDate);

            valuationOperations.Add(new ValuationOperation(
                op.InstrumentId.Value,
                op.Type,
                op.Quantity,
                priceInAccounting,
                feeInAccounting,
                op.TradeDate,
                op.CreatedAt));
        }

        var valuation = new ValuationService().Evaluate(valuationOperations, valuationMethod, assumeSorted: true);
        var positionCosts = valuation.Positions;

        var cashDtos = new List<CashBalanceDto>();
        var cashBase = 0m;
        foreach (var kvp in cashByCurrency)
        {
            var amountBase = data.Convert(kvp.Value, kvp.Key, baseCurrency, asOfDate);
            cashDtos.Add(new CashBalanceDto
            {
                CurrencyId = kvp.Key.ToUpperInvariant(),
                Amount = kvp.Value,
                AmountInBase = amountBase
            });
            cashBase += amountBase;
        }

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
        var annualizedReturnPct = MoneyWeightedReturnCalculator.CalculateAnnualizedReturn(
            operations,
            data,
            baseCurrency,
            navBase,
            asOfDate);

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
            var startSnapshot = CalculateSummary(portfolio, operations, instruments, data, valuationMethod, baseCurrency, startBoundary);
            var endSnapshot = CalculateSummary(portfolio, operations, instruments, data, valuationMethod, baseCurrency, monthEnd);
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
        if (factor <= 0 || !positions.TryGetValue(instrumentId, out var existing))
        {
            return;
        }

        var updated = existing * factor;
        positions[instrumentId] = factor < 1m
            ? decimal.Round(updated, 0, MidpointRounding.AwayFromZero)
            : updated;
    }

}
