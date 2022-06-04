﻿using AutoMapper;
using Larchik.Application.Contracts;
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
    private readonly IUserAccessor _userAccessor;
    private readonly Dictionary<string, Func<Stock, decimal, DealDto, CancellationToken, Task>> _operations;

    public DealService(ILogger<DealService> logger, DataContext context, IMapper mapper, IUserAccessor userAccessor)
    {
        _logger = logger;
        _context = context;
        _mapper = mapper;
        _userAccessor = userAccessor;
        _operations = new Dictionary<string, Func<Stock, decimal, DealDto, CancellationToken, Task>>
        {
            {ListOperations.Add, OperationAddAsync},
            {ListOperations.Commission, OperationAddAsync},
            {ListOperations.Dividends, OperationAddAsync},
            {ListOperations.Tax, OperationAddAsync},
            {ListOperations.Withdrawal, OperationAddAsync},
            {ListOperations.Purchase, OperationPurchaseAsync},
            {ListOperations.Sale, OperationPurchaseAsync}
        };
    }

    public async Task<OperationResult<Unit>> CreateDeal(DealDto dealDto, CancellationToken cancellationToken)
    {
        var account = await _context.Accounts
            .Include(x => x.User)
            .FirstOrDefaultAsync(x => x.Id == dealDto.AccountId && x.User.UserName == _userAccessor.GetUsername(), cancellationToken);
        
        if (account == null) return OperationResult<Unit>.Failure("Счет не найден");
        
        var stock = await _context.Stocks.FirstOrDefaultAsync(x => x.Ticker == dealDto.Stock, cancellationToken);
        
        if (stock == null) return OperationResult<Unit>.Failure("Тикер не найден");
        
        var amount = OperationHelper.GetAmount(dealDto.Operation, dealDto.Quantity, dealDto.Price, dealDto.Commission);
        
        await _operations[dealDto.Operation](stock, amount, dealDto, cancellationToken);

        var deal = _mapper.Map<Deal>(dealDto, opt => { opt.Items["Amount"] = amount; });
        
        _context.Deals.Add(deal);
        await _context.SaveChangesAsync(cancellationToken);
        
        return OperationResult<Unit>.Success(Unit.Value);
    }

    public async Task<OperationResult<Unit>> EditDeal(DealDto dealDto, CancellationToken cancellationToken)
    {
        var deal = await _context.Deals.Include(x => x.Stock).FirstOrDefaultAsync(x => x.Id == dealDto.Id, cancellationToken);
        
        if (deal == null) return OperationResult<Unit>.Failure("Сделка не найдена");
        
        var account = await _context.Accounts
            .Include(x => x.User)
            .FirstOrDefaultAsync(x => x.Id == dealDto.AccountId && x.User.UserName == _userAccessor.GetUsername(), cancellationToken);
        
        if (account == null) return OperationResult<Unit>.Failure("Счет не найден");
        
        var stock = await _context.Stocks.FirstOrDefaultAsync(x => x.Ticker == dealDto.Stock, cancellationToken);
        
        if (stock == null) return OperationResult<Unit>.Failure("Тикер не найден");
        
        await RollbackAssetAsync(deal, cancellationToken);
        
        var amount = OperationHelper.GetAmount(dealDto.Operation, dealDto.Quantity, dealDto.Price, dealDto.Commission);

        await _operations[dealDto.Operation](stock, amount, dealDto, cancellationToken);

        _mapper.Map(dealDto, deal, opt => { opt.Items["Amount"] = amount; });
        
        await _context.SaveChangesAsync(cancellationToken);
        
        return OperationResult<Unit>.Success(Unit.Value);
    }

    public async Task<OperationResult<Unit>> DeleteDeal(Guid id, CancellationToken cancellationToken)
    {
        var deal = await _context.Deals.Include(x => x.Stock).FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        
        if (deal == null) return OperationResult<Unit>.Failure("Сделка не найдена");
        
        await RollbackAssetAsync(deal, cancellationToken);

        _context.Remove(deal);

        await _context.SaveChangesAsync(cancellationToken);
        
        return OperationResult<Unit>.Success(Unit.Value);
    }

    private async Task OperationAddAsync(Stock stock, decimal amount, DealDto dealDto, CancellationToken cancellationToken)
    {
        await AddOrUpdateAssetAsync(stock.CurrencyId, amount, dealDto.AccountId, cancellationToken);
    }

    private async Task OperationPurchaseAsync(Stock stock, decimal amount, DealDto dealDto, CancellationToken cancellationToken)
    {
        await AddOrUpdateAssetAsync(stock.CurrencyId, amount, dealDto.AccountId, cancellationToken);
        var quantity = OperationHelper.GetAssetQuantity(dealDto.Operation, dealDto.Quantity);
        await AddOrUpdateAssetAsync(stock.Ticker, quantity, dealDto.AccountId, cancellationToken);
    }

    private async Task AddOrUpdateAssetAsync(string ticker, decimal quantity, Guid accountId, CancellationToken cancellationToken)
    {
        var asset = await _context.Assets.FirstOrDefaultAsync(x => x.AccountId == accountId && x.StockId == ticker, cancellationToken);
        
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
        if (deal.Stock.TypeId != "MONEY")
        {
            var quantity = OperationHelper.GetAssetQuantity(deal.OperationId, deal.Quantity);
            var asset = await _context.Assets.FirstAsync(x => x.AccountId == deal.AccountId && x.StockId == deal.StockId, cancellationToken);
            asset.Quantity += -quantity;
        }

        var assetMoney = await _context.Assets.FirstAsync(x => x.AccountId == deal.AccountId && x.StockId == deal.Stock.CurrencyId, cancellationToken);
        assetMoney.Quantity += -deal.Amount;
    }
}