using System;

namespace Larchik.Application.Models;

public record PriceModel(Guid InstrumentId, DateTime Date, decimal Value, string CurrencyId, string Provider);
