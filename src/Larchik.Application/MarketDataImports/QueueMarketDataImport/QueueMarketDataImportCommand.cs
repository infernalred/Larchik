using Larchik.Persistence.Entities;

namespace Larchik.Application.MarketDataImports.QueueMarketDataImport;

public sealed record QueueMarketDataImportCommand(
    PriceSource Source,
    string Isin,
    DateOnly FromDate,
    string? IdempotencyKey = null);
