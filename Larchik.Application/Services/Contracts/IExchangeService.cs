using Larchik.Persistence.Entity;

namespace Larchik.Application.Services.Contracts;

public interface IExchangeService
{
    Task<decimal> GetAmountAsync(Operation operation, string code);
}