using Larchik.Application.Models;

namespace Larchik.Application.Currencies.UpdateCurrency;

public record UpdateCurrencyCommand(string Id, UpdateCurrencyModel Model);
