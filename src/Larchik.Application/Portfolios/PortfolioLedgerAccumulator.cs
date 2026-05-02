using Larchik.Application.Helpers;
using Larchik.Application.Models;
using Larchik.Application.Portfolios.Valuation;
using Larchik.Persistence.Entities;

namespace Larchik.Application.Portfolios;

/// <summary>
/// Shared operation walk used by <see cref="PortfolioAnalyticsCalculator"/> and snapshot summary (cash / flows / valuation inputs).
/// </summary>
public static class PortfolioLedgerAccumulator
{
    public sealed record Accumulation(
        Dictionary<string, decimal> CashByCurrency,
        Dictionary<Guid, decimal> Positions,
        List<ValuationOperation> ValuationOperations,
        decimal NetInflowBase,
        decimal GrossDepositsBase,
        decimal GrossWithdrawalsBase);

    public static Accumulation Accumulate(
        Portfolio portfolio,
        IReadOnlyList<Operation> operations,
        IReadOnlyDictionary<Guid, Instrument> instruments,
        HistoricalDataLookup data,
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
                        ApplySplitFactor(op.InstrumentId.Value, op.Quantity, positions, op.Type, op.CreatedAt);
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

        return new Accumulation(cashByCurrency, positions, valuationOperations, netInflowBase, grossDepositsBase, grossWithdrawalsBase);
    }

    public static (List<CashBalanceDto> Dtos, decimal CashBase) BuildCashBalanceDtos(
        IReadOnlyDictionary<string, decimal> cashByCurrency,
        HistoricalDataLookup data,
        string baseCurrency,
        DateTime asOfDate)
    {
        var cashDtos = new List<CashBalanceDto>();
        decimal cashBase = 0m;
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

        return (cashDtos, cashBase);
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

    private static void ApplySplitFactor(
        Guid instrumentId,
        decimal factor,
        IDictionary<Guid, decimal> positions,
        OperationType operationType,
        DateTime createdAt)
    {
        if (factor <= 0 || !positions.TryGetValue(instrumentId, out var existing))
        {
            return;
        }

        var updated = existing * factor;
        if (operationType == OperationType.ReverseSplit &&
            !CorporateActionOperationMetadata.IsSynthetic(createdAt))
        {
            updated = decimal.Round(updated, 0, MidpointRounding.AwayFromZero);
        }

        positions[instrumentId] = updated;
    }
}
