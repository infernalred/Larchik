using Larchik.Application.Helpers;
using Larchik.Persistence.Context;
using Larchik.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace Larchik.Application.Currencies.GetCurrencies;

public class GetCurrenciesQueryHandler(LarchikContext context)
{
    public async Task<Result<Currency[]>> Handle(GetCurrenciesQuery request, CancellationToken cancellationToken)
    {
        var result = await context.Currencies
            .OrderBy(x => x.Id)
            .ToArrayAsync(cancellationToken);

        return Result<Currency[]>.Success(result);
    }
}
