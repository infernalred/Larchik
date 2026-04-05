using Larchik.Application.Helpers;
using MediatR;

namespace Larchik.Application.Operations.DeleteOperation;

public record DeleteOperationCommand(Guid Id) : IRequest<Result<Unit>>;
