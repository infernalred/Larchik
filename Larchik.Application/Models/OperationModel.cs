using System;
using Larchik.Persistence.Entities;

namespace Larchik.Application.Models;

public record OperationModel(
    Guid? InstrumentId,
    OperationType Type,
    decimal Quantity,
    decimal Price,
    decimal Fee,
    string CurrencyId,
    DateTime TradeDate,
    DateTime? SettlementDate,
    string? Note);
