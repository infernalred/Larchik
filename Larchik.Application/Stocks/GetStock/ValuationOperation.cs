using Larchik.Persistence.Entities;

namespace Larchik.Application.Stocks.GetStock;

public record ValuationOperation(
    Guid InstrumentId,
    OperationType Type,
    decimal Quantity,
    decimal Price,
    decimal Fee,
    DateTime TradeDate,
    DateTime CreatedAt);
