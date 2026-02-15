using FluentValidation;
using Larchik.Application.Models;
using Larchik.Persistence.Entities;

namespace Larchik.Application.Operations.Validators;

public class OperationValidator : AbstractValidator<OperationModel>
{
    public OperationValidator()
    {
        RuleFor(x => x.Type).IsInEnum();
        RuleFor(x => x.InstrumentId)
            .NotEmpty()
            .When(x => x.Type is OperationType.Buy or OperationType.Sell or OperationType.BondPartialRedemption or OperationType.BondMaturity)
            .WithMessage("InstrumentId is required for instrument operations.");
        RuleFor(x => x.Quantity).GreaterThan(0);
        RuleFor(x => x.Price).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Fee).GreaterThanOrEqualTo(0);
        RuleFor(x => x.CurrencyId).NotEmpty().Length(3);
        RuleFor(x => x.TradeDate).NotEmpty();
    }
}
