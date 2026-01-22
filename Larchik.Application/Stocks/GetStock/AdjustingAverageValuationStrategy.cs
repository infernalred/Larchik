using Larchik.Persistence.Entities;

namespace Larchik.Application.Valuations;

public class AdjustingAverageValuationStrategy
{
    public Dictionary<Guid, PositionCost> Compute(IEnumerable<Operation> operations)
    {
        var result = new Dictionary<Guid, PositionCost>();

        foreach (var op in operations)
        {
            if (op.InstrumentId is null) continue;

            var instrumentId = op.InstrumentId.Value;
            if (!result.TryGetValue(instrumentId, out var position))
            {
                position = new PositionCost { InstrumentId = instrumentId };
                result[instrumentId] = position;
            }

            var qtyChange = 0m;
            var costChange = 0m;

            switch (op.Type)
            {
                case OperationType.Buy:
                    qtyChange = op.Quantity;
                    costChange = -(op.Quantity * op.Price + op.Fee);
                    break;
                case OperationType.Sell:
                    qtyChange = -op.Quantity;
                    costChange = op.Quantity * op.Price - op.Fee;
                    break;
                case OperationType.TransferIn:
                    qtyChange = op.Quantity;
                    break;
                case OperationType.TransferOut:
                    qtyChange = -op.Quantity;
                    break;
                default:
                    continue;
            }

            position.Quantity += qtyChange;
            position.RollingCost += costChange;
        }

        return result;
    }
}
