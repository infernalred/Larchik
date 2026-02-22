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
            .When(x => OperationTypeRules.RequiresInstrument(x.Type))
            .WithMessage("Instrument is required for instrument operations.");
        RuleFor(x => x.Quantity).GreaterThan(0);
        RuleFor(x => x.Quantity)
            .NotEqual(1m)
            .When(x => x.Type is OperationType.Split or OperationType.ReverseSplit)
            .WithMessage("Split factor must be different from 1.");
        RuleFor(x => x.Price).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Price)
            .Equal(0)
            .When(x => x.Type is OperationType.Split or OperationType.ReverseSplit)
            .WithMessage("Price must be 0 for split operations.");
        RuleFor(x => x.Fee).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Fee)
            .Equal(0)
            .When(x => x.Type is OperationType.Split or OperationType.ReverseSplit)
            .WithMessage("Fee must be 0 for split operations.");
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
