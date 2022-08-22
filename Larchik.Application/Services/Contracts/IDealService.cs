using Larchik.Application.Dtos;
using Larchik.Application.Helpers;
using MediatR;

namespace Larchik.Application.Services.Contracts;

public interface IDealService
{
    Task<OperationResult<Unit>> CreateDeal(DealDto deal, CancellationToken cancellationToken);
    Task<OperationResult<Unit>> EditDeal(DealDto deal, CancellationToken cancellationToken);
    Task<OperationResult<Unit>> DeleteDeal(Guid id, CancellationToken cancellationToken);
}