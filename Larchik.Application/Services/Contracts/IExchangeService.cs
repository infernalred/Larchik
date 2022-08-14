using Larchik.Domain;

namespace Larchik.Application.Services.Contracts;

public interface IExchangeService
{
    Task<decimal> GetAmountAsync(Deal deal, string code);
}