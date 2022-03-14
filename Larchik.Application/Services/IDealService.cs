using Larchik.Application.Dtos;
using Larchik.Application.Helpers;
using MediatR;

namespace Larchik.Application.Services;

public interface IDealService
{
    Task<OperationResult<Unit>> CreateDeal(Guid accountId, DealDto deal, CancellationToken cancellationToken);
    Task<OperationResult<Unit>> EditDeal(Guid accountId, DealDto deal, CancellationToken cancellationToken);
    Task<OperationResult<Unit>> DeleteDeal(Guid accountId, Guid dealId, CancellationToken cancellationToken);
}