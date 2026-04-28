using Larchik.Application.Helpers;
using Larchik.Persistence.Entities;

namespace Larchik.Application.Portfolios.Valuation;

public abstract class LotValuationStrategyBase : IValuationStrategy
{
    private sealed class Lot
    {
        public decimal Quantity { get; set; }
        public decimal CostPerUnit { get; set; }
    }

    public ValuationResult Evaluate(IEnumerable<ValuationOperation> operations)
    {
        var result = new ValuationResult();
        var lotsByInstrument = new Dictionary<Guid, List<Lot>>();

        foreach (var operation in operations)
        {
            var position = GetOrCreatePosition(result, operation.InstrumentId);
            var lots = GetOrCreateLots(lotsByInstrument, operation.InstrumentId);

            switch (operation.Type)
            {
                case OperationType.Buy:
                    ApplyBuy(position, lots, operation);
                    break;

                case OperationType.BondPartialRedemption:
                    ApplyBondPartialRedemption(position, lots, operation);
                    break;

                case OperationType.Sell:
                case OperationType.BondMaturity:
                    ApplyDisposition(result, position, lots, operation);
                    break;

                case OperationType.TransferIn:
                    ApplyTransferIn(position, lots, operation);
                    break;

                case OperationType.TransferOut:
                    ApplyTransferOut(position, lots, operation);
                    break;

                case OperationType.Split:
                case OperationType.ReverseSplit:
                    ApplySplit(position, lots, operation);
                    break;

                default:
                    continue;
            }
        }

        foreach (var (instrumentId, position) in result.Positions)
        {
            if (!lotsByInstrument.TryGetValue(instrumentId, out var lots) || lots.Count == 0)
            {
                position.RollingCost = 0m;
                continue;
            }

            var totalQuantity = lots.Sum(x => x.Quantity);
            var totalCost = lots.Sum(x => x.Quantity * x.CostPerUnit);
            position.Quantity = totalQuantity;
            position.RollingCost = -totalCost;
        }

        return result;
    }

    protected abstract int GetConsumptionIndex(int lotCount);

    protected abstract IEnumerable<int> GetTraversalIndices(int lotCount);

    private static PositionCost GetOrCreatePosition(ValuationResult result, Guid instrumentId)
    {
        if (result.Positions.TryGetValue(instrumentId, out var position))
        {
            return position;
        }

        position = new PositionCost { InstrumentId = instrumentId };
        result.Positions[instrumentId] = position;
        return position;
    }

    private static List<Lot> GetOrCreateLots(IDictionary<Guid, List<Lot>> lotsByInstrument, Guid instrumentId)
    {
        if (lotsByInstrument.TryGetValue(instrumentId, out var lots))
        {
            return lots;
        }

        lots = [];
        lotsByInstrument[instrumentId] = lots;
        return lots;
    }

    private static void ApplyBuy(PositionCost position, ICollection<Lot> lots, ValuationOperation operation)
    {
        var totalCost = operation.Quantity * operation.Price + operation.Fee;
        lots.Add(new Lot
        {
            Quantity = operation.Quantity,
            CostPerUnit = totalCost / operation.Quantity
        });
        position.Quantity += operation.Quantity;
        position.RollingCost -= totalCost;
    }

    private static void ApplyTransferIn(PositionCost position, ICollection<Lot> lots, ValuationOperation operation)
    {
        if (operation.Quantity <= 0)
        {
            return;
        }

        lots.Add(new Lot
        {
            Quantity = operation.Quantity,
            CostPerUnit = 0m
        });
        position.Quantity += operation.Quantity;
    }

    private void ApplyTransferOut(PositionCost position, IList<Lot> lots, ValuationOperation operation)
    {
        if (operation.Quantity <= 0)
        {
            return;
        }

        EnsureAvailableQuantity(position.Quantity, operation.Quantity, operation.Type, position.InstrumentId);
        if (lots.Count == 0)
        {
            return;
        }

        var quantityToTransfer = operation.Quantity;
        var totalCostBefore = lots.Sum(x => x.Quantity * x.CostPerUnit);
        var recipientLots = new HashSet<Lot>();

        while (quantityToTransfer > 0 && lots.Count > 0)
        {
            var lotIndex = GetConsumptionIndex(lots.Count);
            var lot = lots[lotIndex];
            var take = Math.Min(quantityToTransfer, lot.Quantity);

            lot.Quantity -= take;
            quantityToTransfer -= take;

            if (lot.Quantity == 0)
            {
                lots.RemoveAt(lotIndex);
                continue;
            }

            recipientLots.Add(lot);
        }

        if (lots.Count == 0)
        {
            position.Quantity = 0m;
            position.RollingCost = 0m;
            return;
        }

        var retainedCost = totalCostBefore;
        var recipients = recipientLots.Count != 0 ? recipientLots.ToList() : lots.ToList();
        var recipientSet = recipients.ToHashSet();
        var nonRecipientCost = lots
            .Where(x => !recipientSet.Contains(x))
            .Sum(x => x.Quantity * x.CostPerUnit);
        var recipientTargetCost = retainedCost - nonRecipientCost;
        RedistributeCost(recipientTargetCost, recipients);
        position.Quantity = lots.Sum(x => x.Quantity);
        position.RollingCost = -retainedCost;
    }

    private void ApplyBondPartialRedemption(PositionCost position, IReadOnlyList<Lot> lots, ValuationOperation operation)
    {
        var remaining = operation.Quantity;

        foreach (var index in GetTraversalIndices(lots.Count))
        {
            if (remaining <= 0)
            {
                break;
            }

            var lot = lots[index];
            var take = Math.Min(remaining, lot.Quantity);
            lot.CostPerUnit = Math.Max(0, lot.CostPerUnit - operation.Price);
            remaining -= take;
        }

        position.RollingCost += operation.Quantity * operation.Price - operation.Fee;
    }

    private void ApplyDisposition(
        ValuationResult result,
        PositionCost position,
        IList<Lot> lots,
        ValuationOperation operation)
    {
        EnsureAvailableQuantity(position.Quantity, operation.Quantity, operation.Type, position.InstrumentId);

        var remaining = operation.Quantity;
        var costOut = 0m;

        while (remaining > 0 && lots.Count > 0)
        {
            var lotIndex = GetConsumptionIndex(lots.Count);
            var lot = lots[lotIndex];
            var take = Math.Min(remaining, lot.Quantity);
            costOut += take * lot.CostPerUnit;

            lot.Quantity -= take;
            remaining -= take;

            if (lot.Quantity == 0)
            {
                lots.RemoveAt(lotIndex);
            }
        }

        if (remaining > 0)
        {
            throw new InvalidOperationException(
                $"Operation '{operation.Type}' for instrument '{position.InstrumentId}' exceeds available quantity.");
        }

        var proceeds = operation.Quantity * operation.Price - operation.Fee;
        var realized = proceeds - costOut;

        position.Quantity -= operation.Quantity;
        position.RollingCost += costOut;
        RealizedPnlAccumulator.Add(result, operation.InstrumentId, realized);
    }

    private static void ApplySplit(PositionCost position, IReadOnlyList<Lot> lots, ValuationOperation operation)
    {
        var factor = operation.Quantity;
        if (factor <= 0)
        {
            return;
        }

        var totalCostBefore = lots.Sum(x => x.Quantity * x.CostPerUnit);
        var targetTotal = position.Quantity * factor;
        if (operation.Type == OperationType.ReverseSplit &&
            !CorporateActionOperationMetadata.IsSynthetic(operation.CreatedAt))
        {
            // Legacy imported reverse split operations were historically rounded by brokers.
            targetTotal = decimal.Round(targetTotal, 0, MidpointRounding.AwayFromZero);
        }

        position.Quantity = targetTotal;

        if (lots.Count == 0)
        {
            return;
        }

        var lotTotal = 0m;
        foreach (var lot in lots)
        {
            lot.Quantity *= factor;
            lot.CostPerUnit /= factor;
            lotTotal += lot.Quantity;
        }

        var delta = targetTotal - lotTotal;
        if (delta != 0)
        {
            lots[^1].Quantity += delta;
            RedistributeCost(totalCostBefore, lots);
        }
    }

    private static void RedistributeCost(decimal targetCost, IReadOnlyList<Lot> lots)
    {
        if (lots.Count == 0)
        {
            return;
        }

        var totalQuantity = lots.Sum(x => x.Quantity);
        if (totalQuantity <= 0)
        {
            return;
        }

        var assignedCost = 0m;
        for (var i = 0; i < lots.Count; i++)
        {
            var lot = lots[i];
            var allocatedCost = i == lots.Count - 1
                ? targetCost - assignedCost
                : targetCost * (lot.Quantity / totalQuantity);

            lot.CostPerUnit = allocatedCost / lot.Quantity;
            assignedCost += allocatedCost;
        }
    }

    private static void EnsureAvailableQuantity(decimal available, decimal requested, OperationType operationType, Guid instrumentId)
    {
        if (requested <= available)
        {
            return;
        }

        throw new InvalidOperationException(
            $"Operation '{operationType}' for instrument '{instrumentId}' exceeds available quantity: requested {requested}, available {available}.");
    }
}
