namespace Larchik.Application.Portfolios.Valuation;

public interface IValuationStrategy
{
    ValuationResult Evaluate(IEnumerable<ValuationOperation> operations);
}
