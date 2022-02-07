using FluentValidation;
using Larchik.Application.Dtos;

namespace Larchik.Application.BrokerAccounts;

public class BrokerAccountValidator : AbstractValidator<BrokerAccountCreateDto>
{
    public BrokerAccountValidator()
    {
        RuleFor(x => x.BrokerId).NotEmpty();
    }
}