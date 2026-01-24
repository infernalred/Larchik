namespace Larchik.Application.Stocks.GetStock;

public interface IValuationStrategy
{
    ValuationResult Evaluate(IEnumerable<ValuationOperation> operations);
}
