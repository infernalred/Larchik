using Larchik.Persistence.Entities;

namespace Larchik.Application.Stocks.GetStock;

public class StaticAverageValuationStrategy : IValuationStrategy
{
    public ValuationResult Evaluate(IEnumerable<ValuationOperation> operations)
    {
        var result = new ValuationResult();

        foreach (var op in operations.OrderBy(o => o.TradeDate).ThenBy(o => o.CreatedAt))
        {
            var instrumentId = op.InstrumentId;
            if (!result.Positions.TryGetValue(instrumentId, out var position))
            {
                position = new PositionCost { InstrumentId = instrumentId };
                result.Positions[instrumentId] = position;
            }

            var realized = 0m;

            switch (op.Type)
            {
                case OperationType.Buy:
                    position.RollingCost -= op.Quantity * op.Price + op.Fee;
                    position.Quantity += op.Quantity;
                    break;
                case OperationType.Sell:
                    var avg = position.Quantity != 0 ? -position.RollingCost / position.Quantity : 0;
                    realized = op.Quantity * op.Price - op.Fee - avg * op.Quantity;
                    position.Quantity -= op.Quantity;
                    position.RollingCost = -avg * position.Quantity;
                    break;
                case OperationType.TransferIn:
                    position.Quantity += op.Quantity;
                    break;
                case OperationType.TransferOut:
                    position.Quantity -= op.Quantity;
                    break;
                default:
                    continue;
            }

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
