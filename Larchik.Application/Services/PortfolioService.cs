// using System.Collections.Concurrent;
// using AutoMapper;
// using Larchik.Application.Contracts;
// using Larchik.Application.Dtos;
// using Larchik.Application.Portfolios;
// using Larchik.Application.Services.Contracts;
// using Larchik.Domain;
// using Larchik.Domain.Enum;
// using Larchik.Persistence.Context;
// using Larchik.Persistence.Models;
// using Microsoft.EntityFrameworkCore;
// using Microsoft.Extensions.Logging;
//
// namespace Larchik.Application.Services;
//
// public class PortfolioService : IPortfolioService
// {
//     private readonly ILogger<PortfolioService> _logger;
//     private readonly DataContext _context;
//     private readonly IUserAccessor _userAccessor;
//     private readonly IExchangeService _exchangeService;
//     private readonly IMapper _mapper;
//
//     public PortfolioService(ILogger<PortfolioService> logger, DataContext context, IUserAccessor userAccessor,
//         IExchangeService exchangeService, IMapper mapper)
//     {
//         _logger = logger;
//         _context = context;
//         _userAccessor = userAccessor;
//         _exchangeService = exchangeService;
//         _mapper = mapper;
//     }
//
//     public async Task<Portfolio> GetPortfolioAsync(CancellationToken cancellationToken)
//     {
//         var portfolio = new Portfolio();
//
//         var accounts = await _context.Accounts
//             .Where(x => x.User.UserName == _userAccessor.GetUsername())
//             .Include(x => x.Deals)
//             .ToListAsync(cancellationToken);
//
//         var assetsByAccounts = await _context.Assets
//             .Where(x => x.Quantity != 0 && accounts.Contains(x.Account))
//             .Include(x => x.Stock)
//             .ToListAsync(cancellationToken);
//
//         var deals = accounts.SelectMany(x => x.Deals).ToList();
//
//         await CalculationPortfolio(portfolio, assetsByAccounts, deals, cancellationToken);
//
//         return portfolio;
//     }
//
//     public async Task<Portfolio> GetPortfolioAsync(Guid id, CancellationToken cancellationToken)
//     {
//         var portfolio = new Portfolio();
//
//         var account = await _context.Accounts
//             .Where(x => x.User.UserName == _userAccessor.GetUsername() && x.Id == id)
//             .Include(x => x.Deals)
//             .FirstAsync(cancellationToken);
//
//         var assetsByAccounts = await _context.Assets
//             .Where(x => x.Quantity != 0 && x.AccountId == account.Id)
//             .Include(x => x.Stock)
//             .ToListAsync(cancellationToken);
//
//         var deals = account.Deals.ToList();
//
//         await CalculationPortfolio(portfolio, assetsByAccounts, deals, cancellationToken);
//
//         return portfolio;
//     }
//
//     private async Task CalculationPortfolio(Portfolio portfolio, IEnumerable<Asset> assets,
//         IReadOnlyCollection<Deal> deals, CancellationToken cancellationToken)
//     {
//         var currencyExchange = await _context.Stock
//             .Where(x => x.Type == StockKind.Money)
//             .ToDictionaryAsync(x => x.Ticker, x => x, cancellationToken);
//
//         var assetsGroup =
//             from a in assets
//             group a by a.Stock.Ticker
//             into g
//             select new Asset
//             {
//                 StockId = g.Key,
//                 Quantity = g.Sum(x => x.Quantity),
//                 Stock = g.First().Stock
//             };
//
//         var portfolioAssets = new ConcurrentBag<PortfolioAsset>();
//
//         Parallel.ForEach(assetsGroup, asset =>
//         {
//             var dealsByTicker = deals
//                 .Where(x => x.StockId == asset.StockId &&
//                             x.TypeId is (int)DealKind.Purchase or (int)DealKind.Sale)
//                 .OrderBy(x => x.CreatedAt);
//
//             portfolioAssets.Add(GetPortfolioAsset(asset, new Queue<Deal>(dealsByTicker), currencyExchange));
//         });
//
//         portfolio.Assets.AddRange(portfolioAssets.OrderBy(x => x.Stock.Type));
//
//         var inOutMoney = await GeInOutMoney(deals, "RUB");
//
//         portfolio.Profit = portfolio.TotalBalance - inOutMoney;
//     }
//
//     private PortfolioAsset GetPortfolioAsset(Asset asset, Queue<Deal> deals,
//         IReadOnlyDictionary<string, Stock> currencyExchange)
//     {
//         var average = GetAveragePrice(deals);
//
//         currencyExchange.TryGetValue(asset.Stock.CurrencyId, out var currencyStock);
//
//         var rate = currencyStock == null
//             ? 1m
//             : new decimal(currencyStock.LastPrice);
//
//         return new PortfolioAsset(_mapper.Map<StockDto>(asset.Stock), rate, asset.Quantity, Math.Round(average, 2));
//     }
//
//     private static decimal GetAveragePrice(Queue<Deal> deals)
//     {
//         var queueData = new Queue<Deal>();
//         while (deals.Count > 0)
//         {
//             var deal = deals.Dequeue();
//             switch ((DealKind)deal.TypeId)
//             {
//                 case DealKind.Purchase:
//                     queueData.Enqueue(deal);
//                     break;
//                 case DealKind.Sale:
//                     var first = queueData.Peek();
//                     if (first.Quantity > deal.Quantity)
//                     {
//                         first.Quantity -= deal.Quantity;
//                     }
//                     else
//                     {
//                         deal.Quantity -= first.Quantity;
//                         deals.Enqueue(deal);
//                         queueData.Dequeue();
//                     }
//
//                     break;
//             }
//         }
//
//         var average = 1.00m;
//
//         if (queueData.Any())
//         {
//             var totalAmount = 0m;
//             var quantity = 0m;
//
//             foreach (var deal in queueData)
//             {
//                 totalAmount += deal.Quantity * deal.Price;
//                 quantity += deal.Quantity;
//             }
//
//             average = totalAmount / quantity;
//         }
//
//         return average;
//     }
//
//     private async Task<decimal> GeInOutMoney(IEnumerable<Deal> deals, string currencyId)
//     {
//         var moneyDeals = deals
//             .Where(x => x.TypeId != (int)DealKind.Purchase && x.TypeId != (int)DealKind.Sale);
//
//         var result = 0m;
//
//         foreach (var deal in moneyDeals)
//         {
//             if (deal.CurrencyId == currencyId)
//             {
//                 result += deal.Amount;
//             }
//             else
//             {
//                 result += await _exchangeService.GetAmountAsync(deal, $"{deal.CurrencyId}_{currencyId}");
//             }
//         }
//
//         return Math.Round(result, 2);
//     }
// }