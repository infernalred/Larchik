namespace Larchik.API.Services;

internal static class AccountInputNormalizer
{
    public static string NormalizeEmail(string? email) => email?.Trim() ?? string.Empty;

    public static string NormalizeUserName(string? userName) => userName?.Trim() ?? string.Empty;
}
