using Larchik.Domain;
using Larchik.Persistence.Models;

namespace Larchik.Application.Services.Contracts;

public interface IExchangeService
{
    Task<decimal> GetAmountAsync(Operation operation, string code);
}