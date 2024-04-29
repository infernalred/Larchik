using FluentValidation;
using Larchik.Application.Models;

namespace Larchik.Application.Stocks.CreateStock;

public class StockValidator : AbstractValidator<StockModel>
{
    public StockValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(60);
        RuleFor(x => x.Ticker).NotEmpty().MaximumLength(8);
        RuleFor(x => x.Isin).NotEmpty().MaximumLength(12);
        RuleFor(x => x.CurrencyId).NotEmpty().MaximumLength(3);
    }
}