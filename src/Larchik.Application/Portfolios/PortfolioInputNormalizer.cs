namespace Larchik.Application.Portfolios;

internal static class PortfolioInputNormalizer
{
    public static string NormalizeName(string name) => name.Trim();

    public static string? NormalizeCurrencyId(string? currencyId)
    {
        if (string.IsNullOrWhiteSpace(currencyId))
        {
            return null;
        }

        var normalized = currencyId.Trim().ToUpperInvariant();
        return normalized.Length == 3 ? normalized : null;
    }
}
