namespace Larchik.Application.Currencies;

internal static class CurrencyInputNormalizer
{
    public static string? NormalizeId(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim().ToUpperInvariant();
        if (normalized.Length != 3 || !IsAsciiLetterCode(normalized))
        {
            return null;
        }

        return normalized;
    }

    public static string? NormalizeName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim();
    }

    private static bool IsAsciiLetterCode(string value) =>
        value.All(static c => c is >= 'A' and <= 'Z');
}
