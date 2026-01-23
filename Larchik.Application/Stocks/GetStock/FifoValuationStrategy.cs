using Larchik.Persistence.Entities;

namespace Larchik.Application.Valuations;

public class FifoValuationStrategy : IValuationStrategy
{
    private class Lot
    {
        public decimal Quantity { get; set; }
        public decimal CostPerUnit { get; set; }
    }

    public ValuationResult Evaluate(IEnumerable<Operation> operations)
    {
        var result = new ValuationResult();
        var lotsByInstrument = new Dictionary<Guid, Queue<Lot>>();

        foreach (var op in operations.OrderBy(o => o.TradeDate).ThenBy(o => o.CreatedAt))
        {
            if (op.InstrumentId is null) continue;
            var instrumentId = op.InstrumentId.Value;

            if (!result.Positions.TryGetValue(instrumentId, out var position))
            {
                position = new PositionCost { InstrumentId = instrumentId };
                result.Positions[instrumentId] = position;
            }

            if (!lotsByInstrument.TryGetValue(instrumentId, out var lots))
            {
                lots = new Queue<Lot>();
                lotsByInstrument[instrumentId] = lots;
            }

            switch (op.Type)
            {
                case OperationType.Buy:
                {
                    var totalCost = op.Quantity * op.Price + op.Fee;
                    var costPerUnit = totalCost / op.Quantity;
                    lots.Enqueue(new Lot { Quantity = op.Quantity, CostPerUnit = costPerUnit });
                    position.Quantity += op.Quantity;
                    position.RollingCost -= totalCost;
                    break;
                }
                case OperationType.Sell:
                {
                    var remaining = op.Quantity;
                    var costOut = 0m;
                    while (remaining > 0 && lots.Count > 0)
                    {
                        var lot = lots.Peek();
                        var take = Math.Min(remaining, lot.Quantity);
                        costOut += take * lot.CostPerUnit;
                        lot.Quantity -= take;
                        remaining -= take;
                        if (lot.Quantity == 0) lots.Dequeue();
                    }
                    if (remaining > 0)
                    {
                        costOut += remaining * (lots.Count > 0 ? lots.Peek().CostPerUnit : 0);
                    }

                    var proceeds = op.Quantity * op.Price - op.Fee;
                    var realized = proceeds - costOut;

                    position.Quantity -= op.Quantity;
                    position.RollingCost += costOut - op.Fee;

                    if (result.RealizedByInstrument.TryGetValue(instrumentId, out var existing))
                        result.RealizedByInstrument[instrumentId] = existing + realized;
                    else
                        result.RealizedByInstrument[instrumentId] = realized;
                    break;
                }
                case OperationType.TransferIn:
                    position.Quantity += op.Quantity;
                    break;
                case OperationType.TransferOut:
                    position.Quantity -= op.Quantity;
                    break;
                default:
                    continue;
            }
        }

        foreach (var kvp in result.Positions)
        {
            if (lotsByInstrument.TryGetValue(kvp.Key, out var lots) && lots.Count > 0)
            {
                var totalQty = lots.Sum(l => l.Quantity);
                var totalCost = lots.Sum(l => l.Quantity * l.CostPerUnit);
                kvp.Value.RollingCost = -totalCost;
                kvp.Value.Quantity = totalQty;
            }
        }

        return result;
    }
}
