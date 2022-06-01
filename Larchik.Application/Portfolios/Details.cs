using Larchik.Application.Contracts;
using Larchik.Application.Helpers;
using Larchik.Domain;
using Larchik.Persistence.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Larchik.Application.Portfolios;

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
            
            var accounts = await _context.Accounts
                .AsNoTracking()
                .Where(x => x.User.UserName == _userAccessor.GetUsername())
                .Include(x => x.Assets.Where(a => a.Quantity != 0))
                .ThenInclude(x => x.Stock)
                .Include(x => x.Deals)
                .ToListAsync(cancellationToken);

            var deals = accounts.SelectMany(x => x.Deals).ToList();
            
            var assets =
                from ac in accounts
                from a in ac.Assets
                group a by a.Stock.Ticker into g
                select new Asset
                {
                    StockId = g.Key,
                    Quantity = g.Sum(x => x.Quantity),
                    Stock = g.First().Stock
                };

            foreach (var asset in assets)
            {
                var dealsByTicker = deals.Where(x => x.StockId == asset.StockId && x.OperationId is ListOperations.Purchase or ListOperations.Sale).OrderBy(x => x.CreatedAt);
                portfolio.Assets.Add(AveragePrice(asset, new Queue<Deal>(dealsByTicker)));
            }
            
            return OperationResult<Portfolio>.Success(portfolio);
        }

        private static PortfolioAsset AveragePrice(Asset asset, Queue<Deal> deals)
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
            
            var average = 1.00;

            if (queueData.Any())
            {
                var totalAmount = queueData.Sum(x => x.Quantity * x.Price);
                var quantity = queueData.Sum(x => x.Quantity);
                average = totalAmount / quantity;
            }

            return new PortfolioAsset
            {
                Ticker = asset.Stock.Ticker,
                CompanyName = asset.Stock.CompanyName,
                AveragePrice = average,
                Type = asset.Stock.TypeId,
                Price = asset.Stock.LastPrice,
                Quantity = asset.Quantity,
                Sector = asset.Stock.SectorId
            };
        }
    }
}