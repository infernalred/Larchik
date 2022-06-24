using Larchik.Application.Contracts;
using Larchik.Application.Helpers;
using Larchik.Domain;
using Larchik.Persistence.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Larchik.Application.Portfolio;

public class Details
{
    public class Query : IRequest<OperationResult<Portfolio>> { }

    public class Handler : IRequestHandler<Query, OperationResult<Portfolio>>
    {
        private readonly ILogger<Handler> _logger;
        private readonly DataContext _context;
        private readonly IUserAccessor _userAccessor;
        
        public Handler(ILogger<Handler> logger, DataContext context, IUserAccessor userAccessor)
        {
            _logger = logger;
            _context = context;
            _userAccessor = userAccessor;
        }
        
        public async Task<OperationResult<Portfolio>> Handle(Query request, CancellationToken cancellationToken)
        {
            var portfolio = new Portfolio();

            var currencyExchange = await _context.Stocks
                .Where(x => x.TypeId == "MONEY")
                .ToDictionaryAsync(x => x.Ticker, x => x, cancellationToken);
            
            var accounts = await _context.Accounts
                .AsNoTracking()
                .Where(x => x.User.UserName == _userAccessor.GetUsername())
                .Include(x => x.Deals)
                .ToListAsync(cancellationToken);

            var assetsByAccounts = await _context.Assets
                .AsNoTracking()
                .Where(x => x.Quantity != 0 && accounts.Contains(x.Account))
                .Include(x => x.Stock)
                .ToListAsync(cancellationToken);

            var deals = accounts.SelectMany(x => x.Deals).ToList();
            
            var assets =
                (from a in assetsByAccounts
                group a by a.Stock.Ticker into g
                select new Asset
                {
                    StockId = g.Key,
                    Quantity = g.Sum(x => x.Quantity),
                    Stock = g.First().Stock
                }).OrderBy(x => x.Stock.TypeId);

            foreach (var asset in assets)
            {
                var dealsByTicker = deals
                    .Where(x => x.StockId == asset.StockId && x.OperationId is ListOperations.Purchase or ListOperations.Sale)
                    .OrderBy(x => x.CreatedAt);
                
                portfolio.Assets.Add(AveragePrice(asset, new Queue<Deal>(dealsByTicker), currencyExchange));
            }

            var moneyDeals = deals.Where(x => x.OperationId is not (ListOperations.Purchase or ListOperations.Sale));
            var moneyResult = GetTotalMoneyOperations(moneyDeals, currencyExchange);

            portfolio.Profit = portfolio.TotalBalance - moneyResult;
            
            return OperationResult<Portfolio>.Success(portfolio);
        }

        private static PortfolioAsset AveragePrice(Asset asset, Queue<Deal> deals, IReadOnlyDictionary<string, Stock> currencyExchange)
        {
            var queueData = new Queue<Deal>();
            while (deals.Count > 0)
            {
                var deal = deals.Dequeue();
                switch (deal.OperationId)
                {
                    case ListOperations.Purchase:
                        queueData.Enqueue(deal);
                        break;
                    case ListOperations.Sale:
                        var first = queueData.Peek();
                        if (first.Quantity > deal.Quantity)
                        {
                            first.Quantity -= deal.Quantity;
                        }
                        else
                        {
                            deal.Quantity -= first.Quantity;
                            deals.Enqueue(deal);
                            queueData.Dequeue();
                        }
                        break;
                }
            }
            
            var average = 1.00m;

            if (queueData.Any())
            {
                var totalAmount = queueData.Sum(x => x.Quantity * x.Price);
                var quantity = queueData.Sum(x => x.Quantity);
                average = Math.Round(totalAmount / quantity, 2);
            }

            var price = new decimal(asset.Stock.LastPrice);
            
            currencyExchange.TryGetValue(asset.Stock.CurrencyId, out var stock);

            var amountMarket = Math.Round(asset.Quantity * price, 2);
            var amountAverage = Math.Round(asset.Quantity * average, 2);

            var rate = stock == null ? 1m : new decimal(stock.LastPrice);

            return new PortfolioAsset
            {
                Ticker = asset.Stock.Ticker,
                CompanyName = asset.Stock.CompanyName,
                AveragePrice = average,
                Type = asset.Stock.TypeId,
                Price = price,
                Quantity = asset.Quantity,
                Sector = asset.Stock.SectorId,
                AmountMarket = amountMarket,
                AmountAverage = amountAverage,
                AmountMarketCurrency = Math.Round(amountMarket * rate, 2),
                AmountAverageCurrency = Math.Round(amountAverage * rate, 2)
            };
        }

        private static decimal GetTotalMoneyOperations(IEnumerable<Deal> deals, IReadOnlyDictionary<string, Stock> currencyExchange)
        {
            var result = 0m;

            foreach (var deal in deals)
            {
                currencyExchange.TryGetValue(deal.CurrencyId, out var stock);
                var rate = stock == null ? 1m : new decimal(stock.LastPrice);
                result += deal.Amount * rate;
            }

            return Math.Round(result, 2);
        }
    }
}