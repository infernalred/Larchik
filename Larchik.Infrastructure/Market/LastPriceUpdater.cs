using Larchik.Application.Contracts;
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
        var assets = await _context.Assets.Where(a => a.Quantity != 0).ToListAsync(cancellationToken);
        var tickers = assets.Select(x => x.StockId);
        
        var stocks = await _context.Stocks.Where(x => tickers.Contains(x.Ticker) && !string.IsNullOrEmpty(x.Figi)).ToListAsync(cancellationToken);

        // if (stocks.Count > 0)
        // {
        //     var figis = stocks.Select(x => x.Figi);
        //
        //     var stockPrices = (await _marketAccessor.GetLastPrice(figis, cancellationToken)).ToDictionary(x => x.Figi);
        //
        //     foreach (var stock in stocks)
        //     {
        //         stock.LastPrice = stockPrices[stock.Figi].LastPrice;
        //     }
        //
        //     await _context.SaveChangesAsync(cancellationToken);
        // }
    }
}