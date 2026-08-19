namespace Larchik.Application.MarketDataImports.Processing;

public enum MarketDataImportProcessOutcome
{
    Completed,
    Continue,
    Retry,
    Failed
}

public sealed record MarketDataImportProcessResult(MarketDataImportProcessOutcome Outcome, string? Error = null)
{
    public static MarketDataImportProcessResult Completed() => new(MarketDataImportProcessOutcome.Completed);
    public static MarketDataImportProcessResult Continue() => new(MarketDataImportProcessOutcome.Continue);
    public static MarketDataImportProcessResult Retry(string error) => new(MarketDataImportProcessOutcome.Retry, error);
    public static MarketDataImportProcessResult Failed(string error) => new(MarketDataImportProcessOutcome.Failed, error);
}
