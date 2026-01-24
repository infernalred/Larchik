using Larchik.Application.Helpers;
using Larchik.Application.Models;
using MediatR;

namespace Larchik.Application.Operations.CreateOperation;

public record CreateOperationCommand(Guid PortfolioId, OperationModel Model) : IRequest<Result<Guid>>;
