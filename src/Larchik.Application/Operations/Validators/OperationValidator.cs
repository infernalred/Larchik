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
        RuleFor(x => x)
            .Must(HasValidInstrumentUsage)
            .WithMessage("Selected operation type cannot use an instrument.");
        RuleFor(x => x)
            .Must(HasValidOperationShape)
            .WithMessage("Operation fields do not match the selected operation type.");
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

    private static bool HasValidInstrumentUsage(OperationModel model) => model.Type switch
    {
        OperationType.Deposit or OperationType.Withdraw or OperationType.Fee or OperationType.CashAdjustment =>
            model.InstrumentId is null,
        _ => true
    };

    private static bool HasValidOperationShape(OperationModel model) => model.Type switch
    {
        OperationType.Buy or OperationType.Sell or OperationType.BondPartialRedemption or OperationType.BondMaturity =>
            model.Quantity > 0 && model.Price > 0,

        OperationType.Dividend =>
            model.InstrumentId is not null &&
            model.Quantity == 0 &&
            model.Price > 0 &&
            model.Fee == 0,

        OperationType.Deposit or OperationType.Withdraw or OperationType.CashAdjustment =>
            model.InstrumentId is null &&
            model.Fee == 0 &&
            HasExactlyOnePositiveValue(model.Quantity, model.Price),

        OperationType.Fee =>
            model.InstrumentId is null &&
            model.Quantity == 0 &&
            HasExactlyOnePositiveValue(model.Price, model.Fee),

        OperationType.TransferIn or OperationType.TransferOut when model.InstrumentId is null =>
            model.Fee == 0 &&
            HasExactlyOnePositiveValue(model.Quantity, model.Price),

        OperationType.TransferIn or OperationType.TransferOut =>
            model.Quantity > 0 &&
            model.Price == 0 &&
            model.Fee == 0,

        _ => true
    };

    private static bool HasExactlyOnePositiveValue(params decimal[] values) =>
        values.Count(x => x > 0) == 1;
}
