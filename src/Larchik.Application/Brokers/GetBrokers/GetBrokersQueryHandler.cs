using Larchik.Application.Helpers;
using Larchik.Application.Models;
using Larchik.Persistence.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Larchik.Application.Brokers.GetBrokers;

public class GetBrokersQueryHandler(LarchikContext context) : IRequestHandler<GetBrokersQuery, Result<BrokerDto[]>>
{
    public async Task<Result<BrokerDto[]>> Handle(GetBrokersQuery request, CancellationToken cancellationToken)
    {
        var result = await context.Brokers
            .OrderBy(x => x.Name)
            .Select(x => new BrokerDto(x.Id, x.Name, x.Country))
            .ToArrayAsync(cancellationToken);

        return Result<BrokerDto[]>.Success(result);
    }
}
