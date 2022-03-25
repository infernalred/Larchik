using FluentValidation;
using Larchik.Application.Dtos;

namespace Larchik.Application.Accounts;

public class AccountValidator : AbstractValidator<AccountCreateDto>
{
    public AccountValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(15);
    }
}