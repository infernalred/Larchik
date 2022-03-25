using AutoMapper;
using Larchik.Application.Dtos;
using Larchik.Application.Helpers;
using Larchik.Domain;
using Larchik.Persistence.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Larchik.Application.Services;

public class DealService : IDealService
{
    private readonly ILogger<DealService> _logger;
    private readonly DataContext _context;
    private readonly IMapper _mapper;

    public DealService(ILogger<DealService> logger, DataContext context, IMapper mapper)
    {
        _logger = logger;
        _context = context;
        _mapper = mapper;
    }

    public async Task<OperationResult<Unit>> CreateDeal(Guid accountId, DealDto dealDto, CancellationToken cancellationToken)
    {
        var stock = await _context.Stocks.FirstOrDefaultAsync(x => x.Ticker == dealDto.Stock, cancellationToken);
        
        if (stock == null) return OperationResult<Unit>.Failure("Тикер не найден");
        
        var amount = CurrencyOperation.CreateCurrencyDeal(dealDto.Operation, dealDto.Quantity, dealDto.Price, dealDto.Commission);

        if (stock.TypeId != "MONEY")
        {
            var asset = await _context.Assets.FirstOrDefaultAsync(x => x.StockId == dealDto.Stock, cancellationToken);
            
            var quantity = AssetOperation.CreateAssetDeal(dealDto.Operation, dealDto.Quantity);
            
            if (asset == null)
            {
                asset = new Asset
                {
                    Id = Guid.NewGuid(),
                    AccountId = accountId,
                    Stock = stock,
                    Quantity = quantity
                };

                _context.Assets.Add(asset);
            }
            else
            {
                asset.Quantity += quantity;
            }
        }
        
        var assetMoney = await _context.Assets.FirstOrDefaultAsync(x => x.StockId == stock.CurrencyId, cancellationToken);
        
        if (assetMoney == null)
        {
            assetMoney = new Asset
            {
                Id = Guid.NewGuid(),
                AccountId = accountId,
                StockId = stock.CurrencyId,
                Quantity = amount
            };
            _context.Assets.Add(assetMoney);
        }
        else
        {
            assetMoney.Quantity += amount;
        }

        var deal = _mapper.Map<Deal>(dealDto, opt => { opt.Items["AccountId"] = accountId; opt.Items["Amount"] = amount; });
        
        _context.Deals.Add(deal);
        await _context.SaveChangesAsync(cancellationToken);
        
        return OperationResult<Unit>.Success(Unit.Value);
    }

    public async Task<OperationResult<Unit>> EditDeal(Guid accountId, DealDto dealDto, CancellationToken cancellationToken)
    {
        var deal = await _context.Deals.Include(x => x.Stock).FirstOrDefaultAsync(x => x.Id == dealDto.Id, cancellationToken);
        
        if (deal == null) return OperationResult<Unit>.Failure("Сделка не найдена");
        
        if (deal.Stock.TypeId != "MONEY")
        {
            var quantity = AssetOperation.CreateAssetDeal(deal.OperationId, deal.Quantity);
            var assetOld = await _context.Assets.FirstAsync(x => x.StockId == deal.StockId, cancellationToken);
            assetOld.Quantity += -quantity;
        }

        var assetOldMoney = await _context.Assets.FirstAsync(x => x.StockId == deal.Stock.CurrencyId, cancellationToken);
        assetOldMoney.Quantity += -deal.Amount;
        
        var stock = await _context.Stocks.FirstOrDefaultAsync(x => x.Ticker == dealDto.Stock, cancellationToken);
        
        if (stock == null) return OperationResult<Unit>.Failure("Тикер не найден");
        
        var amount = CurrencyOperation.CreateCurrencyDeal(dealDto.Operation, dealDto.Quantity, dealDto.Price, dealDto.Commission);

        if (stock.TypeId != "MONEY")
        {
            var asset = await _context.Assets.FirstOrDefaultAsync(x => x.StockId == dealDto.Stock, cancellationToken);
            
            var quantity = AssetOperation.CreateAssetDeal(dealDto.Operation, dealDto.Quantity);
            
            if (asset == null)
            {
                asset = new Asset
                {
                    Id = Guid.NewGuid(),
                    AccountId = accountId,
                    Stock = stock,
                    Quantity = quantity
                };

                _context.Assets.Add(asset);
            }
            else
            {
                asset.Quantity += quantity;
            }
        }
        
        var assetMoney = await _context.Assets.FirstOrDefaultAsync(x => x.StockId == stock.CurrencyId, cancellationToken);
        
        if (assetMoney == null)
        {
            assetMoney = new Asset
            {
                Id = Guid.NewGuid(),
                AccountId = accountId,
                StockId = stock.CurrencyId,
                Quantity = amount
            };
            _context.Assets.Add(assetMoney);
        }
        else
        {
            assetMoney.Quantity += amount;
        }

        _mapper.Map(dealDto, deal, opt => { opt.Items["AccountId"] = accountId; opt.Items["Amount"] = amount; });
        
        await _context.SaveChangesAsync(cancellationToken);
        
        return OperationResult<Unit>.Success(Unit.Value);
    }

    public async Task<OperationResult<Unit>> DeleteDeal(Guid accountId, Guid dealId, CancellationToken cancellationToken)
    {
        var deal = await _context.Deals.Include(x => x.Stock).FirstOrDefaultAsync(x => x.Id == dealId, cancellationToken);
        
        if (deal == null) return OperationResult<Unit>.Failure("Сделка не найдена");
        
        if (deal.Stock.TypeId != "MONEY")
        {
            var quantity = AssetOperation.CreateAssetDeal(deal.OperationId, deal.Quantity);
            var asset = await _context.Assets.FirstAsync(x => x.StockId == deal.StockId, cancellationToken);
            asset.Quantity += -quantity;
        }

        var assetMoney = await _context.Assets.FirstAsync(x => x.StockId == deal.Stock.CurrencyId, cancellationToken);
        assetMoney.Quantity += -deal.Amount;

        _context.Remove(deal);

        await _context.SaveChangesAsync(cancellationToken);
        
        return OperationResult<Unit>.Success(Unit.Value);
    }
}