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
                    position.Quantity += operation.Quantity;
                    break;

                case OperationType.TransferOut:
                    position.Quantity -= operation.Quantity;
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
            costOut += remaining * (lots.Count > 0 ? lots[GetConsumptionIndex(lots.Count)].CostPerUnit : 0);
        }

        var proceeds = operation.Quantity * operation.Price - operation.Fee;
        var realized = proceeds - costOut;

        position.Quantity -= operation.Quantity;
        position.RollingCost += costOut - operation.Fee;
        AddRealized(result, operation.InstrumentId, realized);
    }

    private static void ApplySplit(PositionCost position, IReadOnlyList<Lot> lots, ValuationOperation operation)
    {
        var factor = operation.Quantity;
        if (factor <= 0)
        {
            return;
        }

        var scaledTotal = position.Quantity * factor;
        var targetTotal = operation.Type == OperationType.ReverseSplit
            ? decimal.Round(scaledTotal, 0, MidpointRounding.AwayFromZero)
            : scaledTotal;
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
        }
    }

    private static void AddRealized(ValuationResult result, Guid instrumentId, decimal realized)
    {
        if (result.RealizedByInstrument.TryGetValue(instrumentId, out var existing))
        {
            result.RealizedByInstrument[instrumentId] = existing + realized;
            return;
        }

        result.RealizedByInstrument[instrumentId] = realized;
    }
}
