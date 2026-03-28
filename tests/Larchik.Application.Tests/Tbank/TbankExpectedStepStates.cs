using System.Text.Json;
using System.Text.Json.Serialization;

namespace Larchik.Application.Tests.Tbank;

internal sealed class TbankExpectedStepStates
{
    [JsonPropertyName("fileName")]
    public string FileName { get; init; } = null!;

    [JsonPropertyName("cash")]
    public Dictionary<string, decimal> Cash { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    [JsonPropertyName("positions")]
    public List<TbankExpectedState.PositionState> Positions { get; init; } = [];

    public static IReadOnlyDictionary<string, TbankExpectedStepStates> LoadAll()
    {
        var path = Path.Combine(TbankReportFixtureHelper.ReferenceDataRoot, "expected-step-states.json");
        var json = File.ReadAllText(path);
        var states = JsonSerializer.Deserialize<List<TbankExpectedStepStates>>(json, JsonOptions())
                     ?? throw new InvalidOperationException($"Failed to load expected T-Bank step states from {path}.");

        return states.ToDictionary(x => x.FileName, StringComparer.OrdinalIgnoreCase);
    }

    private static JsonSerializerOptions JsonOptions() => new()
    {
        PropertyNameCaseInsensitive = true
    };
}
