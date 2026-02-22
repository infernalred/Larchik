namespace Larchik.Application.Operations;

public static class OperationInputNormalizer
{
    public static DateTime NormalizeUtc(DateTimeOffset value) => value.UtcDateTime;

    public static DateTime? NormalizeUtc(DateTimeOffset? value) =>
        value.HasValue ? NormalizeUtc(value.Value) : null;
}
