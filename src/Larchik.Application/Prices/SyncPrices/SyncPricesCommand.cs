using Larchik.Application.Models;

namespace Larchik.Application.Prices.SyncPrices;

public record SyncPricesCommand(IReadOnlyCollection<PriceModel> Prices);
