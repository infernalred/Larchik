namespace Larchik.Application.Operations;

public static class OperationInputNormalizer
{
    public static DateTime NormalizeUtc(DateTimeOffset value) => value.UtcDateTime;

    public static DateTime? NormalizeUtc(DateTimeOffset? value) =>
        value.HasValue ? NormalizeUtc(value.Value) : null;

    public static string? NormalizeCurrencyId(string? currencyId)
    {
        if (string.IsNullOrWhiteSpace(currencyId))
        {
            return null;
        }

        var normalized = currencyId.Trim().ToUpperInvariant();
        return normalized.Length == 3 ? normalized : null;
    }

    public static string? NormalizeNote(string? note) =>
        string.IsNullOrWhiteSpace(note)
            ? null
            : note.Trim();

    public static string? NormalizeInstrumentCode(string? isin, string? ticker)
    {
        var rawCode = !string.IsNullOrWhiteSpace(isin)
            ? isin
            : ticker;

        return string.IsNullOrWhiteSpace(rawCode)
            ? null
            : rawCode.Trim().ToUpperInvariant();
    }
}
