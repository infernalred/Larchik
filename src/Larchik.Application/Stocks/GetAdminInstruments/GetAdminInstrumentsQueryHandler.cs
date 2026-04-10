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
        var query = context.Instruments
            .AsNoTracking()
            .AsQueryable();

        var input = request.Query?.Trim();
        if (!string.IsNullOrWhiteSpace(input))
        {
            var raw = input.ToUpperInvariant();
            query = query.Where(x =>
                x.Ticker.ToUpper().Contains(raw) ||
                x.Name.ToUpper().Contains(raw) ||
                x.Isin.ToUpper().Contains(raw) ||
                (x.Figi != null && x.Figi.ToUpper().Contains(raw)) ||
                (x.Exchange != null && x.Exchange.ToUpper().Contains(raw)) ||
                (x.Country != null && x.Country.ToUpper().Contains(raw)));
        }

        var result = await query
            .OrderBy(x => x.Type)
            .ThenBy(x => x.Ticker)
            .ThenBy(x => x.Name)
            .Select(x => new InstrumentDto
            {
                Id = x.Id,
                Name = x.Name,
                Ticker = x.Ticker,
                Isin = x.Isin,
                Figi = x.Figi,
                Type = x.Type,
                CurrencyId = x.CurrencyId,
                CategoryId = x.CategoryId,
                Exchange = x.Exchange,
                Country = x.Country,
                IsTrading = x.IsTrading
            })
            .ToPagedResultAsync(request.Paging, MaxPageSize, cancellationToken);

        return Result<PagedResult<InstrumentDto>>.Success(result);
    }
}
