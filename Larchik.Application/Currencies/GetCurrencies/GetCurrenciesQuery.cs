using Larchik.Application.Helpers;
using Larchik.Persistence.Entities;
using MediatR;

namespace Larchik.Application.Currencies.GetCurrencies;

public class GetCurrenciesQuery : IRequest<Result<Currency[]>>;