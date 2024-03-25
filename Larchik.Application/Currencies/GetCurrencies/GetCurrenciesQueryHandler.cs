using Larchik.Application.Helpers;
using Larchik.Persistence.Context;
using Larchik.Persistence.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Larchik.Application.Currencies.GetCurrencies;

public class GetCurrenciesQueryHandler(LarchikContext context) : IRequestHandler<GetCurrenciesQuery, Result<Currency[]>>
{
    public async Task<Result<Currency[]>> Handle(GetCurrenciesQuery request, CancellationToken cancellationToken)
    {
        var result = await context.Currencies
            .ToArrayAsync(cancellationToken);

        return Result<Currency[]>.Success(result);
    }
}