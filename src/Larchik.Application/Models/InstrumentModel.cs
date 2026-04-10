using Larchik.Persistence.Entities;

namespace Larchik.Application.Models;

public record InstrumentModel(
    string Name,
    string Ticker,
    string Isin,
    string? Figi,
    InstrumentType Type,
    string CurrencyId,
    int CategoryId,
    string? Exchange,
    string? Country,
    bool IsTrading,
    PriceSource? PriceSource);
