using Larchik.Application.Helpers;
using Larchik.Application.Models;
using Larchik.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace Larchik.Application.ReferenceData.GetExchanges;

public sealed class GetExchangesQueryHandler(LarchikContext context)
{
    public async Task<Result<IReadOnlyCollection<ReferenceItemDto>>> Handle(
        GetExchangesQuery request,
        CancellationToken cancellationToken)
    {
        var exchanges = await context.Exchanges
            .OrderBy(x => x.Name)
            .ThenBy(x => x.Id)
            .Select(x => new ReferenceItemDto(x.Id, x.Name))
            .ToArrayAsync(cancellationToken);

        return Result<IReadOnlyCollection<ReferenceItemDto>>.Success(exchanges);
    }
}
