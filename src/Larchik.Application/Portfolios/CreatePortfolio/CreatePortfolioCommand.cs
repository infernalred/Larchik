using Larchik.Application.Helpers;
using Larchik.Application.Models;
using MediatR;

namespace Larchik.Application.Portfolios.CreatePortfolio;

public record CreatePortfolioCommand(PortfolioModel Model) : IRequest<Result<Guid>>;
