using Larchik.Application.Helpers;
using Larchik.Application.Models;
using MediatR;

namespace Larchik.Application.Stocks.GetStock;

public record EditOperationCommand(Guid Id, OperationModel Model) : IRequest<Result<Unit>>;
