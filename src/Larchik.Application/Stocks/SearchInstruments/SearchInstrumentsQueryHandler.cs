using Larchik.Application.Helpers;
using Larchik.Application.Models;
using Larchik.Persistence.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Larchik.Application.Stocks.SearchInstruments;

public class SearchInstrumentsQueryHandler(LarchikContext context)
    : IRequestHandler<SearchInstrumentsQuery, Result<InstrumentLookupDto[]>>
{
    public async Task<Result<InstrumentLookupDto[]>> Handle(SearchInstrumentsQuery request, CancellationToken cancellationToken)
    {
        var limit = Math.Clamp(request.Limit, 1, 50);
        var input = request.Query?.Trim();
        IQueryable<Larchik.Persistence.Entities.Instrument> query = context.Instruments;

        if (!string.IsNullOrWhiteSpace(input))
        {
            var rawKey = input.ToUpperInvariant();
            var normalizedKey = InstrumentQueryHelper.NormalizeSearchKey(rawKey);
            var compactKey = normalizedKey.Replace(" ", string.Empty);

            query = InstrumentQueryHelper.ApplyLookupCandidateSearch(query, input);

            var candidates = await InstrumentQueryHelper.ApplyDefaultOrdering(query)
                .Take(200)
                .Select(InstrumentQueryHelper.LookupDtoProjection)
                .ToArrayAsync(cancellationToken);

            var filtered = candidates
                .Where(x => InstrumentQueryHelper.MatchesLookup(x, rawKey, normalizedKey, compactKey))
                .Take(limit)
                .ToArray();

            return Result<InstrumentLookupDto[]>.Success(filtered);
        }

        var instruments = await InstrumentQueryHelper.ApplyDefaultOrdering(query)
            .Take(limit)
            .Select(InstrumentQueryHelper.LookupDtoProjection)
            .ToArrayAsync(cancellationToken);

        return Result<InstrumentLookupDto[]>.Success(instruments);
    }
}
