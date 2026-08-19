using Larchik.Application.Helpers;
using Larchik.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace Larchik.Application.MarketDataImports.GetMarketDataImport;

public sealed class GetMarketDataImportQueryHandler(LarchikContext context)
{
    public async Task<Result<MarketDataImportDto>?> Handle(
        GetMarketDataImportQuery query,
        CancellationToken cancellationToken)
    {
        var request = await context.MarketDataImportRequests
            .FirstOrDefaultAsync(x => x.Id == query.Id, cancellationToken);

        return request is null
            ? null
            : Result<MarketDataImportDto>.Success(MarketDataImportDto.FromEntity(request));
    }
}
