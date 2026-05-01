using Larchik.Application.Helpers;
using Larchik.Application.Models;
using Larchik.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace Larchik.Application.ReferenceData.GetCountries;

public sealed class GetCountriesQueryHandler(LarchikContext context)
{
    public async Task<Result<IReadOnlyCollection<ReferenceItemDto>>> Handle(
        GetCountriesQuery request,
        CancellationToken cancellationToken)
    {
        var countries = await context.Countries
            .OrderBy(x => x.Name)
            .ThenBy(x => x.Id)
            .Select(x => new ReferenceItemDto(x.Id, x.Name))
            .ToArrayAsync(cancellationToken);

        return Result<IReadOnlyCollection<ReferenceItemDto>>.Success(countries);
    }
}
