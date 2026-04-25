using Larchik.Application.Common.Paging;
using Larchik.Application.Helpers;
using Larchik.Application.Models;
using Larchik.Persistence.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Larchik.Application.Stocks.GetAdminInstruments;

public class GetAdminInstrumentsQueryHandler(LarchikContext context)
    : IRequestHandler<GetAdminInstrumentsQuery, Result<PagedResult<InstrumentDto>>>
{
    private const int MaxPageSize = 200;

    public async Task<Result<PagedResult<InstrumentDto>>> Handle(
        GetAdminInstrumentsQuery request,
        CancellationToken cancellationToken)
    {
        var query = InstrumentQueryHelper.ApplyAdminSearch(
            context.Instruments.AsQueryable(),
            request.Query);

        var country = request.Country?.Trim();
        if (!string.IsNullOrWhiteSpace(country))
        {
            var countryPattern = $"%{country}%";
            query = query.Where(x => x.CountryId != null && EF.Functions.ILike(x.CountryId, countryPattern));
        }

        if (request.IsTrading is { } isTrading)
        {
            query = query.Where(x => x.IsTrading == isTrading);
        }

        var result = await InstrumentQueryHelper.ApplyDefaultOrdering(query)
            .Select(InstrumentQueryHelper.AdminDtoProjection)
            .ToPagedResultAsync(request.Paging, MaxPageSize, cancellationToken);

        return Result<PagedResult<InstrumentDto>>.Success(result);
    }
}
