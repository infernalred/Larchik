using FluentValidation;
using Larchik.Application.Dtos;

namespace Larchik.Application.Brokers;

public class BrokerValidator : AbstractValidator<BrokerDto>
{
    public BrokerValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(255);
        RuleFor(x => x.Inn).NotEmpty().MinimumLength(10).MaximumLength(10);
    }
}