using Larchik.Application.Models;
using Larchik.Persistence.Entities;

namespace Larchik.Application.Stocks;

public static class InstrumentInputNormalizer
{
    public static NormalizedInstrumentInput Normalize(InstrumentModel model) =>
        new(
            NormalizeRequiredText(model.Name),
            NormalizeRequiredCode(model.Ticker),
            NormalizeOptionalCode(model.Isin),
            NormalizeOptionalCode(model.Figi),
            model.Type,
            NormalizeRequiredCode(model.CurrencyId),
            model.CategoryId,
            NormalizeOptionalText(model.Exchange),
            NormalizeOptionalText(model.Country),
            model.IsTrading,
            model.PriceSource);

    public static void ApplyTo(Instrument instrument, NormalizedInstrumentInput input)
    {
        instrument.Name = input.Name;
        instrument.Ticker = input.Ticker;
        instrument.Isin = input.Isin;
        instrument.Figi = input.Figi;
        instrument.Type = input.Type;
        instrument.CurrencyId = input.CurrencyId;
        instrument.CategoryId = input.CategoryId;
        instrument.Exchange = input.Exchange;
        instrument.Country = input.Country;
        instrument.IsTrading = input.IsTrading;
        instrument.PriceSource = input.PriceSource;
    }

    private static string NormalizeRequiredText(string value) => value.Trim();

    private static string NormalizeRequiredCode(string value) => value.Trim().ToUpperInvariant();

    private static string? NormalizeOptionalCode(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToUpperInvariant();

    private static string? NormalizeOptionalText(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed record NormalizedInstrumentInput(
    string Name,
    string Ticker,
    string? Isin,
    string? Figi,
    InstrumentType Type,
    string CurrencyId,
    int CategoryId,
    string? Exchange,
    string? Country,
    bool IsTrading,
    PriceSource? PriceSource);
