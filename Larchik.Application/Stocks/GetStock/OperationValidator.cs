using FluentValidation;
using Larchik.Application.Models;

namespace Larchik.Application.Stocks.GetStock;

public class OperationValidator : AbstractValidator<OperationModel>
{
    public OperationValidator()
    {
        RuleFor(x => x.Type).IsInEnum();
        RuleFor(x => x.Quantity).GreaterThan(0);
        RuleFor(x => x.Price).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Fee).GreaterThanOrEqualTo(0);
        RuleFor(x => x.CurrencyId).NotEmpty().Length(3);
        RuleFor(x => x.TradeDate).NotEmpty();
    }
}
