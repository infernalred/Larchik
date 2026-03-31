using System.Text.Json;
using System.Text.Json.Serialization;
using Larchik.Persistence.Entities;

namespace Larchik.Application.Tests.Tbank;

internal sealed class TbankExpectedStepStates
{
    [JsonPropertyName("fileName")]
    public string FileName { get; init; } = null!;

    [JsonPropertyName("importedOperations")]
    public int ImportedOperations { get; init; }

    [JsonPropertyName("skippedOperations")]
    public int SkippedOperations { get; init; }

    [JsonPropertyName("totalOperationsInDb")]
    public int TotalOperationsInDb { get; init; }

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
    public List<TbankExpectedState.PositionState> Positions { get; init; } = [];

    public static IReadOnlyList<TbankExpectedStepStates> LoadAll()
    {
        var path = Path.Combine(TbankReportFixtureHelper.ReferenceDataRoot, "expected-step-states.json");
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<List<TbankExpectedStepStates>>(json, JsonOptions())
               ?? throw new InvalidOperationException($"Failed to load expected T-Bank step states from {path}.");
    }

    private static JsonSerializerOptions JsonOptions() => new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };
}
