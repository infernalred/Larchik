using Larchik.Application.Services.Contracts;
using Larchik.Domain;
using Larchik.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Larchik.Application.Services;

public class ExchangeService : IExchangeService
{
    private readonly DataContext _context;
    private readonly IMemoryCache _cache;

    public ExchangeService(DataContext context, IMemoryCache cache)
    {
        _context = context;
        _cache = cache;
    }

    public async Task<decimal> GetAmountAsync(Deal deal, string code)
    {
        var date = DateOnly.FromDateTime(deal.CreatedAt);
        var key = $"{code}_{date}";

        if (!_cache.TryGetValue(key, out Exchange exchange))
        {
            exchange = await _context.Exchanges
                .OrderByDescending(x => x.Date)
                .FirstAsync(x => x.Code == code && x.Date <= date);

            _cache.Set(key, exchange, new MemoryCacheEntryOptions().SetAbsoluteExpiration(TimeSpan.FromHours(1)));
        }

        return deal.Amount * (decimal)exchange.Rate / exchange.Nominal;
    }
}