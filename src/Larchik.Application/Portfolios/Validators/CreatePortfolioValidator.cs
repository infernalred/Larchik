using FluentValidation;
using Larchik.Application.Models;

namespace Larchik.Application.Portfolios.Validators;

public class CreatePortfolioValidator : AbstractValidator<PortfolioModel>
{
    public CreatePortfolioValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(120);
        RuleFor(x => x.ReportingCurrencyId).NotEmpty().Length(3);
        RuleFor(x => x.BrokerId).NotEmpty();
    }
}
