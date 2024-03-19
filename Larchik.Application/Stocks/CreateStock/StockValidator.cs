using FluentValidation;
using Larchik.Application.Dtos;

namespace Larchik.Application.Stocks.CreateStock;

public class StockValidator : AbstractValidator<StockDto>
{
    public StockValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(60);
        RuleFor(x => x.Ticker).NotEmpty().MaximumLength(8);
        RuleFor(x => x.Isin).NotEmpty().MaximumLength(12);
        RuleFor(x => x.CurrencyId).NotEmpty().MaximumLength(3);
        RuleFor(x => x.SectorId).NotEmpty().MaximumLength(60);
        RuleFor(x => x.LastPrice).LessThanOrEqualTo(0);
    }
}