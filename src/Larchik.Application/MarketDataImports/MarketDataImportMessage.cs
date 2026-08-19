namespace Larchik.Application.MarketDataImports;

public sealed record MarketDataImportMessage(int SchemaVersion, Guid RequestId, Guid CorrelationId)
{
    public const string MessageType = "market-data.import";

    public static MarketDataImportMessage Create(Guid requestId) => new(1, requestId, requestId);
}
