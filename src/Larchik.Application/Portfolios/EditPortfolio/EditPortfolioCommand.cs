using Larchik.Application.Models;

namespace Larchik.Application.Portfolios.EditPortfolio;

public record EditPortfolioCommand(Guid Id, PortfolioModel Model);
