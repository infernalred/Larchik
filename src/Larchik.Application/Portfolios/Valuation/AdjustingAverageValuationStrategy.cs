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
                    qtyChange = -op.Quantity;
                    var avgBefore = position.Quantity != 0 ? -position.RollingCost / position.Quantity : 0;
                    realized = op.Quantity * op.Price - op.Fee - avgBefore * op.Quantity;
                    costChange = op.Quantity * op.Price - op.Fee;
                    break;
                case OperationType.TransferIn:
                    qtyChange = op.Quantity;
                    break;
                case OperationType.TransferOut:
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

            if (realized != 0)
            {
                if (result.RealizedByInstrument.TryGetValue(instrumentId, out var existing))
                {
                    result.RealizedByInstrument[instrumentId] = existing + realized;
                }
                else
                {
                    result.RealizedByInstrument[instrumentId] = realized;
                }
            }
        }

        return result;
    }
}
