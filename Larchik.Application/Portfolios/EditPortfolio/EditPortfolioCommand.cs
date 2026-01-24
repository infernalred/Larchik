using Larchik.Application.Helpers;
using Larchik.Application.Models;
using MediatR;

namespace Larchik.Application.Portfolios.EditPortfolio;

public record EditPortfolioCommand(Guid Id, PortfolioModel Model) : IRequest<Result<Unit>>;
