// using Larchik.Application.Services.Contracts;
// using Larchik.Persistence.Context;
// using Larchik.Persistence.Entity;
// using Microsoft.EntityFrameworkCore;
// using Microsoft.Extensions.Caching.Memory;
//
// namespace Larchik.Application.Services;
//
// public class ExchangeService : IExchangeService
// {
//     private readonly DataContext _context;
//     private readonly IMemoryCache _cache;
//
//     public ExchangeService(DataContext context, IMemoryCache cache)
//     {
//         _context = context;
//         _cache = cache;
//     }
//
//     public async Task<decimal> GetAmountAsync(Operation operation, string code)
//     {
//         var date = DateOnly.FromDateTime(operation.CreatedAt);
//         var key = $"{code}_{date}";
//
//         if (!_cache.TryGetValue(key, out Exchange exchange))
//         {
//             exchange = await _context.Exchanges
//                 .OrderByDescending(x => x.Date)
//                 .FirstAsync(x => x.Code == code && x.Date <= date);
//
//             _cache.Set(key, exchange, new MemoryCacheEntryOptions().SetAbsoluteExpiration(TimeSpan.FromHours(1)));
//         }
//
//         return operation.Amount * (decimal)exchange.Rate / exchange.Nominal;
//     }
// }