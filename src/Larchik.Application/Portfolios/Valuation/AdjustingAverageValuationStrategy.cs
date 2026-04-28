using Larchik.Persistence.Entities;

namespace Larchik.Application.Portfolios.Valuation;

public class AdjustingAverageValuationStrategy : IValuationStrategy
{
    public ValuationResult Evaluate(IEnumerable<ValuationOperation> operations)
    {
        var result = new ValuationResult();

        foreach (var op in operations)
        {
            var instrumentId = op.InstrumentId;
            if (!result.Positions.TryGetValue(instrumentId, out var position))
            {
                position = new PositionCost { InstrumentId = instrumentId };
                result.Positions[instrumentId] = position;
            }

            var qtyChange = 0m;
            var costChange = 0m;
            var realized = 0m;

            switch (op.Type)
            {
                case OperationType.Buy:
                    qtyChange = op.Quantity;
                    costChange = -(op.Quantity * op.Price + op.Fee);
                    break;
                case OperationType.BondPartialRedemption:
                    costChange = op.Quantity * op.Price - op.Fee;
                    break;
                case OperationType.Sell:
                case OperationType.BondMaturity:
                    EnsureAvailableQuantity(position.Quantity, op.Quantity, op.Type, instrumentId);
                    qtyChange = -op.Quantity;
                    var avgBefore = position.Quantity != 0 ? -position.RollingCost / position.Quantity : 0;
                    realized = op.Quantity * op.Price - op.Fee - avgBefore * op.Quantity;
                    costChange = avgBefore * op.Quantity;
                    break;
                case OperationType.TransferIn:
                    qtyChange = op.Quantity;
                    break;
                case OperationType.TransferOut:
                    EnsureAvailableQuantity(position.Quantity, op.Quantity, op.Type, instrumentId);
                    qtyChange = -op.Quantity;
                    break;
                case OperationType.Split:
                case OperationType.ReverseSplit:
                    if (position.Quantity != 0)
                    {
                        var updated = position.Quantity * op.Quantity;
                        if (op.Type == OperationType.ReverseSplit)
                        {
                            updated = decimal.Round(updated, 0, MidpointRounding.AwayFromZero);
                        }

                        qtyChange = updated - position.Quantity;
                    }
                    break;
                default:
                    continue;
            }

            position.Quantity += qtyChange;
            position.RollingCost += costChange;

            if (position.Quantity == 0)
            {
                position.RollingCost = 0;
            }

            RealizedPnlAccumulator.Add(result, instrumentId, realized);
        }

        return result;
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
