using System.Text.Json;
using System.Text.Json.Serialization;
using Larchik.Persistence.Entities;

namespace Larchik.Application.Tests.Tbank;

internal sealed class TbankExpectedState
{
    [JsonPropertyName("operationCount")]
    public int OperationCount { get; init; }

    [JsonPropertyName("cashBase")]
    public decimal CashBase { get; init; }

    [JsonPropertyName("positionsValueBase")]
    public decimal PositionsValueBase { get; init; }

    [JsonPropertyName("navBase")]
    public decimal NavBase { get; init; }

    [JsonPropertyName("realizedBase")]
    public decimal RealizedBase { get; init; }

    [JsonPropertyName("unrealizedBase")]
    public decimal UnrealizedBase { get; init; }

    [JsonPropertyName("netInflowBase")]
    public decimal NetInflowBase { get; init; }

    [JsonPropertyName("breakdown")]
    public Dictionary<OperationType, int> Breakdown { get; init; } = [];

    [JsonPropertyName("cash")]
    public Dictionary<string, decimal> Cash { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    [JsonPropertyName("positions")]
    public List<PositionState> Positions { get; init; } = [];

    public static TbankExpectedState Load(string fileName)
    {
        var path = Path.Combine(TbankReportFixtureHelper.ReferenceDataRoot, fileName);
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<TbankExpectedState>(json, JsonOptions())
               ?? throw new InvalidOperationException($"Failed to load expected T-Bank state from {path}.");
    }

    private static JsonSerializerOptions JsonOptions() => new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    internal sealed class PositionState
    {
        public string Ticker { get; init; } = null!;
        public string InstrumentName { get; init; } = null!;
        public decimal Quantity { get; init; }
        public decimal? LastPrice { get; init; }
        public decimal MarketValueBase { get; init; }
        public decimal AverageCost { get; init; }
        public string CurrencyId { get; init; } = null!;
        public string? PriceCurrencyId { get; init; }
        public string? AverageCostCurrencyId { get; init; }
    }
}
