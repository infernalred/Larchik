using Larchik.Persistence.Entities;

namespace Larchik.Application.MarketDataImports.Processing;

public interface IMarketDataImportSource
{
    PriceSource Source { get; }

    Task<MarketDataSourceResult<ResolvedMarketDataInstrument>> ResolveAsync(
        string isin,
        CancellationToken cancellationToken);

    Task<MarketDataSourceResult<IReadOnlyCollection<MarketDataImportPricePoint>>> LoadPricesAsync(
        MarketDataImportPriceLoadRequest request,
        CancellationToken cancellationToken);
}

public sealed record ResolvedMarketDataInstrument(
    string Name,
    string Ticker,
    string Isin,
    string? Figi,
    InstrumentType Type,
    string CurrencyId,
    string? ExchangeId,
    string? CountryId,
    bool IsTrading,
    string SourceInstrumentCode,
    string? Board,
    string? Engine,
    string? Market,
    DateOnly? ListedFrom);

public sealed record MarketDataImportPriceLoadRequest(
    Guid InstrumentId,
    string Isin,
    string Ticker,
    string? Figi,
    InstrumentType Type,
    string CurrencyId,
    string SourceInstrumentCode,
    string? Board,
    string? Engine,
    string? Market,
    DateOnly FromDate,
    DateOnly ToDate);

public sealed record MarketDataImportPricePoint(
    DateOnly Date,
    decimal Value,
    string CurrencyId,
    string? SourceCurrencyId);

public sealed record MarketDataSourceResult<T>(T? Value, string? Error, bool IsSuccess, bool IsTransient)
{
    public static MarketDataSourceResult<T> Success(T value) => new(value, null, true, false);
    public static MarketDataSourceResult<T> TransientFailure(string error) => new(default, error, false, true);
    public static MarketDataSourceResult<T> PermanentFailure(string error) => new(default, error, false, false);
}
