using Larchik.Application.Helpers;
using MediatR;

namespace Larchik.Application.Portfolios.DeletePortfolio;

public record DeletePortfolioCommand(Guid Id) : IRequest<Result<Unit>>;
