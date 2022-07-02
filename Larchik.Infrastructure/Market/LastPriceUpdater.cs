﻿using Larchik.Application.Contracts;
using Larchik.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Larchik.Infrastructure.Market;

public class LastPriceUpdater
{
    private readonly ILogger<LastPriceUpdater> _logger;
    private readonly DataContext _context;
    private readonly IMarketAccessor _marketAccessor;

    public LastPriceUpdater(ILogger<LastPriceUpdater> logger, DataContext context, IMarketAccessor marketAccessor)
    {
        _logger = logger;
        _context = context;
        _marketAccessor = marketAccessor;
    }

    public async Task UpdateLastPrice(CancellationToken cancellationToken)
    {
        var assets = await _context.Assets
            .Include(x => x.Stock)
            .Where(a => a.Quantity != 0)
            .ToListAsync(cancellationToken);

        var stocks = assets
            .Select(x => x.Stock)
            .DistinctBy(x => x.Ticker)
            .Where(x => !string.IsNullOrEmpty(x.Figi) && x.TypeId != "MONEY")
            .ToList();

        var moneyStocks = await _context.Stocks
            .Where(x => x.TypeId == "MONEY" && !string.IsNullOrEmpty(x.Figi))
            .ToListAsync(cancellationToken);
        
        stocks.AddRange(moneyStocks);

        if (stocks.Count > 0)
        {
            var figis = stocks.Select(x => x.Figi);
        
            var stockPrices = (await _marketAccessor.GetLastPrice(figis, cancellationToken)).ToDictionary(x => x.Figi);
        
            foreach (var stock in stocks)
            {
                stockPrices.TryGetValue(stock.Figi, out var stockPrice);
                if (stockPrice != null) stock.LastPrice = stockPrice.LastPrice;
                
            }
        
            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}