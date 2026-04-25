using FluentValidation;
using Larchik.Application.Models;
using Larchik.Persistence.Entities;

namespace Larchik.Application.Stocks.CreateStock;

public class InstrumentValidator : AbstractValidator<InstrumentModel>
{
    public InstrumentValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(120);
        RuleFor(x => x.Ticker).NotEmpty().MaximumLength(16);
        RuleFor(x => x.Isin).MaximumLength(12);
        RuleFor(x => x.Isin)
            .NotEmpty()
            .When(x => InstrumentRules.RequiresIsin(x.Type))
            .WithMessage("ISIN is required for equity, bond, and ETF instruments.");
        RuleFor(x => x.Figi).MaximumLength(32);
        RuleFor(x => x.Type).IsInEnum();
        RuleFor(x => x.CurrencyId).NotEmpty().MaximumLength(3);
        RuleFor(x => x.Exchange).MaximumLength(50);
        RuleFor(x => x.Country).MaximumLength(100);
        RuleFor(x => x.PriceSource).IsInEnum().When(x => x.PriceSource.HasValue);
        RuleFor(x => x.Figi)
            .NotEmpty()
            .When(x => x.IsTrading && x.PriceSource == Persistence.Entities.PriceSource.TBANK)
            .WithMessage("FIGI is required for TBANK price source.");
        RuleFor(x => x.PriceSource)
            .Must((model, _) => !UsesForbiddenTbankSource(model))
            .WithMessage("Russian equities, bonds, ETFs, and FX instruments must use MOEX price source.");
    }

    private static bool UsesForbiddenTbankSource(InstrumentModel model)
    {
        if (!model.IsTrading || model.PriceSource != Persistence.Entities.PriceSource.TBANK)
        {
            return false;
        }

        var country = model.Country?.Trim().ToUpperInvariant();
        var isRussianMarketType = model.Type is InstrumentType.Equity or InstrumentType.Bond or InstrumentType.Etf or InstrumentType.Currency;

        return country == "RU" && isRussianMarketType;
    }
}
