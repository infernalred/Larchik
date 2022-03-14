using FluentValidation;
using Larchik.Application.Dtos;

namespace Larchik.Application.Deals;

public class DealValidator : AbstractValidator<DealDto>
{
    public DealValidator()
    {
        RuleFor(x => x.Quantity).GreaterThan(0);
        RuleFor(x => x.Price).GreaterThan(0);
        RuleFor(x => x.Stock).NotEmpty();
        RuleFor(x => x.Operation).NotEmpty();
    }
}