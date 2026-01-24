using Larchik.Application.Helpers;
using MediatR;

namespace Larchik.Application.Stocks.GetStock;

public record DeletePortfolioCommand(Guid Id) : IRequest<Result<Unit>>;
