using Larchik.Application.Helpers;
using Larchik.Application.Models;
using MediatR;

namespace Larchik.Application.Operations.GetOperation;

public record GetOperationQuery(Guid Id) : IRequest<Result<OperationDto?>>;
