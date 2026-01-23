using Larchik.Persistence.Entities;

namespace Larchik.Application.Valuations;

public interface IValuationStrategy
{
    ValuationResult Evaluate(IEnumerable<Operation> operations);
}
