namespace Larchik.Persistence.Entities;

public enum MarketDataImportStatus
{
    Queued = 1,
    ResolvingInstrument = 2,
    LoadingPrices = 3,
    Succeeded = 4,
    SkippedExisting = 5,
    Failed = 6
}
