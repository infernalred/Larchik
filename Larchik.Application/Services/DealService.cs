using AutoMapper;
using Larchik.Application.Contracts;
using Larchik.Application.Dtos;
using Larchik.Application.Helpers;
using Larchik.Application.Services.Contracts;
using Larchik.Domain;
using Larchik.Domain.Enum;
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
    private readonly IUserAccessor _userAccessor;
    private readonly Dictionary<DealKind, Func<decimal, DealDto, CancellationToken, Task>> _operations;

    public DealService(ILogger<DealService> logger, DataContext context, IMapper mapper, IUserAccessor userAccessor)
    {
        _logger = logger;
        _context = context;
        _mapper = mapper;
        _userAccessor = userAccessor;
        _operations = new Dictionary<DealKind, Func<decimal, DealDto, CancellationToken, Task>>
        {
            {DealKind.Add, OperationAddAsync},
            {DealKind.Commission, OperationAddAsync},
            {DealKind.Dividends, OperationAddAsync},
            {DealKind.Tax, OperationAddAsync},
            {DealKind.Withdrawal, OperationAddAsync},
            {DealKind.Purchase, OperationPurchaseAsync},
            {DealKind.Sale, OperationPurchaseAsync}
        };
    }

    public async Task<Result<Unit>> CreateDeal(DealDto dealDto, CancellationToken cancellationToken)
    {
        var user = await _context.Users
            .AsTracking()
            .FirstAsync(x => x.UserName == _userAccessor.GetUsername(), cancellationToken: cancellationToken);
        
        var account = await _context.Accounts
            .AsTracking()
            .Include(x => x.User)
            .FirstOrDefaultAsync(x => x.Id == dealDto.AccountId && x.User == user, cancellationToken);
        
        if (account == null) return Result<Unit>.Failure("Счет не найден");
        
        var amount = OperationHelper.GetAmount(dealDto.Type, dealDto.Quantity, dealDto.Price, dealDto.Commission);
        
        await _operations[dealDto.Type](amount, dealDto, cancellationToken);

        var deal = _mapper.Map<Deal>(dealDto, opt => { opt.Items["Amount"] = amount; });
        deal.UserId = user.Id;
        
        _context.Deals.Add(deal);
        await _context.SaveChangesAsync(cancellationToken);
        
        return Result<Unit>.Success(Unit.Value);
    }

    public async Task<Result<Unit>> EditDeal(DealDto dealDto, CancellationToken cancellationToken)
    {
        var deal = await _context.Deals
            .AsTracking()
            .Include(x => x.Stock)
            .FirstAsync(x => x.Id == dealDto.Id, cancellationToken);
        
        var account = await _context.Accounts
            .Include(x => x.User)
            .FirstOrDefaultAsync(x => x.Id == dealDto.AccountId 
                                      && x.User.UserName == _userAccessor.GetUsername(), cancellationToken);
        
        if (account == null) return Result<Unit>.Failure("Счет не найден");
        
        await RollbackAssetAsync(deal, cancellationToken);
        
        var amount = OperationHelper.GetAmount(dealDto.Type, dealDto.Quantity, dealDto.Price, dealDto.Commission);

        await _operations[dealDto.Type](amount, dealDto, cancellationToken);

        _mapper.Map(dealDto, deal, opt => { opt.Items["Amount"] = amount; });
        
        await _context.SaveChangesAsync(cancellationToken);
        
        return Result<Unit>.Success(Unit.Value);
    }

    public async Task<Result<Unit>> DeleteDeal(Guid id, CancellationToken cancellationToken)
    {
        var deal = await _context.Deals
            .AsTracking()
            .Include(x => x.Stock)
            .FirstAsync(x => x.Id == id, cancellationToken);
        
        await RollbackAssetAsync(deal, cancellationToken);

        _context.Remove(deal);

        await _context.SaveChangesAsync(cancellationToken);
        
        return Result<Unit>.Success(Unit.Value);
    }

    private async Task OperationAddAsync(decimal amount, DealDto dealDto, CancellationToken cancellationToken)
    {
        await AddOrUpdateAssetAsync(dealDto.Currency, amount, dealDto.AccountId, cancellationToken);
    }

    private async Task OperationPurchaseAsync(decimal amount, DealDto dealDto, CancellationToken cancellationToken)
    {
        await AddOrUpdateAssetAsync(dealDto.Currency, amount, dealDto.AccountId, cancellationToken);

        if (dealDto.Stock != null)
        {
            var quantity = OperationHelper.GetAssetQuantity(dealDto.Type, dealDto.Quantity);
            await AddOrUpdateAssetAsync(dealDto.Stock, quantity, dealDto.AccountId, cancellationToken);
        }
    }

    private async Task AddOrUpdateAssetAsync(string ticker, decimal quantity, Guid accountId, CancellationToken cancellationToken)
    {
        var asset = await _context.Assets
            .AsTracking()
            .FirstOrDefaultAsync(x => x.AccountId == accountId && x.StockId == ticker, cancellationToken);
        
        if (asset == null)
        {
            asset = new Asset
            {
                Id = Guid.NewGuid(),
                AccountId = accountId,
                StockId = ticker,
                Quantity = quantity
            };

            _context.Assets.Add(asset);
        }
        else
        {
            asset.Quantity += quantity;
        }
    }

    private async Task RollbackAssetAsync(Deal deal, CancellationToken cancellationToken)
    {
        if (deal.TypeId is (int)DealKind.Purchase or (int)DealKind.Sale)
        {
            var quantity = OperationHelper.GetAssetQuantity((DealKind)deal.TypeId, deal.Quantity);
            
            var asset = await _context.Assets
                .AsTracking()
                .FirstAsync(x => x.AccountId == deal.AccountId && x.StockId == deal.StockId, cancellationToken);
            
            asset.Quantity += -quantity;
        }

        var assetMoney = await _context.Assets
            .AsTracking()
            .FirstAsync(x => x.AccountId == deal.AccountId && x.StockId == deal.CurrencyId, cancellationToken);
        
        assetMoney.Quantity += -deal.Amount;
    }
}