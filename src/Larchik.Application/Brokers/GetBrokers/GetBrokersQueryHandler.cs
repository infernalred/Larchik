using Larchik.Application.Helpers;
using Larchik.Application.Models;
using Larchik.Application.Operations.ImportBroker;
using Larchik.Persistence.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Larchik.Application.Brokers.GetBrokers;

public class GetBrokersQueryHandler(LarchikContext context, IEnumerable<IBrokerReportParser> parsers)
    : IRequestHandler<GetBrokersQuery, Result<BrokerDto[]>>
{
    public async Task<Result<BrokerDto[]>> Handle(GetBrokersQuery request, CancellationToken cancellationToken)
    {
        var supportedBrokerCodes = parsers
            .Select(x => x.Code)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var brokers = await context.Brokers
            .OrderBy(x => x.Name)
            .ToArrayAsync(cancellationToken);

        var result = brokers
            .Select(x => new BrokerDto(
                x.Id,
                x.Code,
                x.Name,
                x.Country,
                x.Code is not null && supportedBrokerCodes.Contains(x.Code)))
            .ToArray();

        return Result<BrokerDto[]>.Success(result);
    }
}
