using FluentValidation;
using Larchik.Application.Models;
using Larchik.Persistence.Entities;

namespace Larchik.Application.Operations.Validators;

public class OperationValidator : AbstractValidator<OperationModel>
{
    public OperationValidator()
    {
        RuleFor(x => x.Type).IsInEnum();
        RuleFor(x => x.Type)
            .Must(type => OperationTypeRules.IsVisibleInPortfolioOperations(type))
            .WithMessage("Split and reverse split are administrative corporate actions and cannot be created in a portfolio.");
        RuleFor(x => x.InstrumentId)
            .NotEmpty()
            .When(x => OperationTypeRules.RequiresInstrument(x.Type))
            .WithMessage("Instrument is required for instrument operations.");
        RuleFor(x => x.Quantity)
            .GreaterThan(0)
            .When(x => OperationTypeRules.RequiresPositiveQuantity(x.Type));
        RuleFor(x => x.Quantity)
            .GreaterThanOrEqualTo(0)
            .When(x => OperationTypeRules.AllowsZeroQuantity(x.Type));
        RuleFor(x => x.Price).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Fee).GreaterThanOrEqualTo(0);
        RuleFor(x => x.CurrencyId).NotEmpty().Length(3);
        RuleFor(x => x.TradeDate).NotEmpty();
        RuleFor(x => x.TradeDate)
            .Must(x => x.Offset == TimeSpan.Zero)
            .WithMessage("TradeDate must be in UTC (ISO format with 'Z').");
        RuleFor(x => x.SettlementDate)
            .Must(x => !x.HasValue || x.Value.Offset == TimeSpan.Zero)
            .WithMessage("SettlementDate must be in UTC (ISO format with 'Z').");
        RuleFor(x => x.SettlementDate)
            .Must((model, settlementDate) =>
                !settlementDate.HasValue ||
                settlementDate.Value.UtcDateTime >= model.TradeDate.UtcDateTime)
            .When(x => x.SettlementDate.HasValue)
            .WithMessage("SettlementDate must be greater than or equal to TradeDate.");
    }
}
