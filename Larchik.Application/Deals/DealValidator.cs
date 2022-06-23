using FluentValidation;
using Larchik.Application.Dtos;
using Larchik.Application.Helpers;

namespace Larchik.Application.Deals;

public class DealValidator : AbstractValidator<DealDto>
{
    public DealValidator()
    {
        RuleFor(x => x.Quantity).GreaterThan(0);
        RuleFor(x => x.Price).GreaterThan(0);
        RuleFor(x => x.Currency).NotEmpty();
        RuleFor(x => x.Stock).NotEmpty().When(x => x.Operation is ListOperations.Purchase or ListOperations.Sale);
        RuleFor(x => x.Operation).NotEmpty();
    }
}