using System.Text.Json;
using Larchik.Persistence.Entities;

namespace Larchik.Application.Tests.Tbank;

internal sealed class TbankReferenceData
{
    public IReadOnlyList<CurrencySeed> Currencies { get; init; } = [];
    public IReadOnlyList<CategorySeed> Categories { get; init; } = [];
    public IReadOnlyList<InstrumentSeed> Instruments { get; init; } = [];
    public IReadOnlyList<AliasSeed> Aliases { get; init; } = [];
    public IReadOnlyList<CorporateActionSeed> CorporateActions { get; init; } = [];
    public IReadOnlyList<PriceSeed> Prices { get; init; } = [];
    public IReadOnlyList<FxRateSeed> FxRates { get; init; } = [];

    public static TbankReferenceData Load()
    {
        var json = File.ReadAllText(Path.Combine(TbankReportFixtureHelper.ReferenceDataRoot, "reference-data.json"));
        return JsonSerializer.Deserialize<TbankReferenceData>(json, JsonOptions())
               ?? throw new InvalidOperationException("Failed to load T-Bank reference data.");
    }

    public static IReadOnlyList<ManualOperationSeed> LoadManualOperations()
    {
        var json = File.ReadAllText(Path.Combine(TbankReportFixtureHelper.ReferenceDataRoot, "manual-operations.json"));
        return JsonSerializer.Deserialize<List<ManualOperationSeed>>(json, JsonOptions())
               ?? throw new InvalidOperationException("Failed to load manual T-Bank operations.");
    }

    private static JsonSerializerOptions JsonOptions() => new()
    {
        PropertyNameCaseInsensitive = true
    };

    internal sealed record CurrencySeed(string Id);
    internal sealed record CategorySeed(int Id, string Name);
    internal sealed record InstrumentSeed(
        Guid Id,
        string Name,
        string Ticker,
        string Isin,
        string? Figi,
        InstrumentType Type,
        string CurrencyId,
        int CategoryId,
        string? Exchange,
        string? Country,
        bool IsTrading);
    internal sealed record AliasSeed(Guid InstrumentId, string AliasCode, string NormalizedAliasCode);
    internal sealed record CorporateActionSeed(Guid InstrumentId, OperationType Type, decimal Factor, DateTime EffectiveDate, string Note);
    internal sealed record PriceSeed(
        Guid InstrumentId,
        DateTime Date,
        decimal Value,
        string CurrencyId,
        string? SourceCurrencyId,
        string Provider);
    internal sealed record FxRateSeed(
        string BaseCurrencyId,
        string QuoteCurrencyId,
        DateTime Date,
        decimal Rate,
        string Source);
    internal sealed record ManualOperationSeed(
        DateTime TradeDate,
        DateTime? SettlementDate,
        string? Ticker,
        OperationType Type,
        decimal Quantity,
        decimal Price,
        decimal Fee,
        string CurrencyId,
        string? Note);
}
