using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Larchik.Persistence.Entities;

namespace Larchik.Application.Operations.ImportBroker;

internal static class BrokerOperationKeyBuilder
{
    public static string Build(Operation operation, string? instrumentCode, int occurrence)
    {
        return $"v2:{BuildBaseHash(operation, instrumentCode)}:{occurrence.ToString("D6", CultureInfo.InvariantCulture)}";
    }

    public static string BuildBaseHash(Operation operation, string? instrumentCode)
    {
        var payload = string.Join('|',
            "v1",
            ((int)operation.Type).ToString(CultureInfo.InvariantCulture),
            (instrumentCode ?? string.Empty).Trim(),
            operation.Quantity.ToString("0.000000", CultureInfo.InvariantCulture),
            operation.Price.ToString("0.000000", CultureInfo.InvariantCulture),
            operation.Fee.ToString("0.0000", CultureInfo.InvariantCulture),
            operation.CurrencyId,
            FormatDate(operation.TradeDate),
            FormatNullableDate(operation.SettlementDate),
            NormalizeNote(operation.Note));

        var hash = MD5.HashData(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string FormatDate(DateTime value)
    {
        var utc = value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };

        return utc.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }

    private static string FormatNullableDate(DateTime? value) => value is null ? string.Empty : FormatDate(value.Value);

    private static string NormalizeNote(string? value) => (value ?? string.Empty).Trim();
}
