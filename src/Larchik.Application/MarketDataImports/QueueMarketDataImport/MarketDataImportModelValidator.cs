using FluentValidation;

namespace Larchik.Application.MarketDataImports.QueueMarketDataImport;

public sealed class MarketDataImportModelValidator : AbstractValidator<MarketDataImportModel>
{
    public MarketDataImportModelValidator()
    {
        RuleFor(x => x.Source).IsInEnum();
        RuleFor(x => x.Isin)
            .NotEmpty()
            .Length(12)
            .Must(IsValidIsin)
            .WithMessage("ISIN is invalid.");
        RuleFor(x => x.FromDate)
            .NotEmpty()
            .WithMessage("From date is required.")
            .LessThanOrEqualTo(_ => DateOnly.FromDateTime(DateTime.UtcNow))
            .WithMessage("From date cannot be in the future.");
    }

    internal static bool IsValidIsin(string? rawValue)
    {
        if (string.IsNullOrWhiteSpace(rawValue)) return false;

        var value = rawValue.Trim().ToUpperInvariant();
        if (value.Length != 12 || value.Any(x => !char.IsAsciiLetterUpper(x) && !char.IsAsciiDigit(x)))
        {
            return false;
        }

        var expanded = string.Concat(value.Select(x => char.IsAsciiLetterUpper(x) ? (x - 'A' + 10).ToString() : x.ToString()));
        var sum = 0;
        var doubleDigit = false;
        for (var index = expanded.Length - 1; index >= 0; index--)
        {
            var digit = expanded[index] - '0';
            if (doubleDigit)
            {
                digit *= 2;
                digit = digit / 10 + digit % 10;
            }

            sum += digit;
            doubleDigit = !doubleDigit;
        }

        return sum % 10 == 0;
    }
}
