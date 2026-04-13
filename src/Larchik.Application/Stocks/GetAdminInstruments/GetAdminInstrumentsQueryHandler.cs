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
            var pattern = $"%{input}%";
            query = query.Where(x =>
                EF.Functions.ILike(x.Ticker, pattern) ||
                EF.Functions.ILike(x.Name, pattern) ||
                EF.Functions.ILike(x.Isin, pattern) ||
                (x.Figi != null && EF.Functions.ILike(x.Figi, pattern)) ||
                (x.Exchange != null && EF.Functions.ILike(x.Exchange, pattern)) ||
                (x.Country != null && EF.Functions.ILike(x.Country, pattern)));
        }

        var country = request.Country?.Trim();
        if (!string.IsNullOrWhiteSpace(country))
        {
            var countryPattern = $"%{country}%";
            query = query.Where(x => x.Country != null && EF.Functions.ILike(x.Country, countryPattern));
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
                IsTrading = x.IsTrading,
                PriceSource = x.PriceSource
            })
            .ToPagedResultAsync(request.Paging, MaxPageSize, cancellationToken);

        return Result<PagedResult<InstrumentDto>>.Success(result);
    }
}
