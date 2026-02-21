using Larchik.Application.Helpers;
using Larchik.Application.Models;
using MediatR;

namespace Larchik.Application.Operations.GetOperations;

public record GetOperationsQuery(Guid PortfolioId) : IRequest<Result<IReadOnlyCollection<OperationDto>>>;
