using Larchik.Persistence.Entities;

namespace Larchik.Application.Models;

public record OperationModel(
    Guid? InstrumentId,
    OperationType Type,
    decimal Quantity,
    decimal Price,
    decimal Fee,
    string CurrencyId,
    DateTimeOffset TradeDate,
    DateTimeOffset? SettlementDate,
    string? Note);
