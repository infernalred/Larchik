using Larchik.Persistence.Entities;

namespace Larchik.Application.MarketDataImports.QueueMarketDataImport;

public sealed record MarketDataImportModel(PriceSource Source, string Isin, DateOnly FromDate);
