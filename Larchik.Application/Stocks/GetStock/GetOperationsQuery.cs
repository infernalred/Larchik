using Larchik.Application.Helpers;
using Larchik.Application.Models;
using MediatR;

namespace Larchik.Application.Stocks.GetStock;

public record GetOperationsQuery(Guid PortfolioId) : IRequest<Result<IReadOnlyCollection<OperationDto>>>;
