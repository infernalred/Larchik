using Larchik.Application.Helpers;
using MediatR;

namespace Larchik.Application.Stocks.GetStock;

public record DeleteOperationCommand(Guid Id) : IRequest<Result<Unit>>;
