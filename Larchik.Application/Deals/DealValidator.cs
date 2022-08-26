using FluentValidation;
using Larchik.Application.Dtos;
using Larchik.Domain.Enum;

namespace Larchik.Application.Deals;

public class DealValidator : AbstractValidator<DealDto>
{
    public DealValidator()
    {
        RuleFor(x => x.Quantity).GreaterThan(0);
        RuleFor(x => x.Price).GreaterThan(0);
        RuleFor(x => x.Currency).NotEmpty();
        RuleFor(x => x.Stock).NotEmpty().When(x => x.Type is DealKind.Purchase or DealKind.Sale);
        //RuleFor(x => x.Type).NotEmpty();
    }
}