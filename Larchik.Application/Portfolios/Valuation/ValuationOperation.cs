using Larchik.Persistence.Entities;

namespace Larchik.Application.Portfolios.Valuation;

public record ValuationOperation(
    Guid InstrumentId,
    OperationType Type,
    decimal Quantity,
    decimal Price,
    decimal Fee,
    DateTime TradeDate,
    DateTime CreatedAt);
