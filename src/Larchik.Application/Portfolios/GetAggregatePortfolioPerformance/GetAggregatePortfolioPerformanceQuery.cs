namespace Larchik.Application.Portfolios.GetAggregatePortfolioPerformance;

public record GetAggregatePortfolioPerformanceQuery(
    string? Method = null,
    string? Currency = null,
    DateTime? From = null,
    DateTime? To = null);

