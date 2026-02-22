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
        var query = context.Instruments.AsNoTracking();
        var input = request.Query?.Trim();

        if (!string.IsNullOrWhiteSpace(input))
        {
            var key = input.ToUpperInvariant();
            query = query.Where(x => x.Ticker.ToUpper().StartsWith(key));
        }

        var instruments = await query
            .OrderBy(x => x.Ticker)
            .ThenBy(x => x.Name)
            .Take(limit)
            .Select(x => new InstrumentLookupDto
            {
                Id = x.Id,
                Name = x.Name,
                Ticker = x.Ticker,
                Isin = x.Isin,
                Figi = x.Figi,
                CurrencyId = x.CurrencyId
            })
            .ToArrayAsync(cancellationToken);

        return Result<InstrumentLookupDto[]>.Success(instruments);
    }
}
