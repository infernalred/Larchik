﻿using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using Larchik.Application.Contracts;
using Larchik.Application.Dtos;
using Larchik.Application.Helpers;
using Larchik.Domain;
using Larchik.Domain.Enum;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Larchik.Application.Tests.DealService;

public class CreateDealTests
{
    private readonly MapperConfiguration _mapperConfiguration = new(cfg => cfg.AddProfile(new MappingProfiles()));
    
    [Fact]
    public async Task CreateDeal_Failure_AccountIsNull()
    {
        var mockUserAccessor = new Mock<IUserAccessor>();
        var logger = new Mock<ILogger<Services.DealService>>();
        var context = SetupDbContext.Generate();
        var mapper = new Mock<IMapper>();

        var deal = new DealDto
        {
            Id = Guid.NewGuid(),
            AccountId = Guid.NewGuid(),
            CreatedAt = DateTime.Now,
            Type = DealKind.Add,
            Price = 500,
            Quantity = 1,
            Stock = "RUB"
        };
        
        mockUserAccessor.Setup(x => x.GetUsername()).Returns("admin");
        
        var dealService = new Services.DealService(logger.Object, context, mapper.Object, mockUserAccessor.Object);

        var actual = await dealService.CreateDeal(deal, CancellationToken.None);
        
        Assert.False(actual.IsSuccess);
        Assert.Equal(Unit.Value, actual.Result);
        Assert.NotNull(actual.Error);
        Assert.Equal("Счет не найден", actual.Error);
        mockUserAccessor.Verify(x => x.GetUsername());
        mockUserAccessor.VerifyNoOtherCalls();
        mockUserAccessor.VerifyAll();
        logger.VerifyNoOtherCalls();
        logger.VerifyAll();
    }
    
    [Fact]
    public async Task CreateDeal_Failure_StockIsNotExist()
    {
        var mockUserAccessor = new Mock<IUserAccessor>();
        var logger = new Mock<ILogger<Services.DealService>>();
        var context = SetupDbContext.Generate();
        var mapper = new Mock<IMapper>();
    
        var deal = new DealDto
        {
            Id = Guid.NewGuid(),
            AccountId = Guid.Parse("f1fe6744-86a6-4293-b469-64404511840f"),
            CreatedAt = DateTime.Now,
            Type = DealKind.Add,
            Price = 500,
            Quantity = 1,
            Currency = "RUB1"
        };
        
        mockUserAccessor.Setup(x => x.GetUsername()).Returns("admin");
        
        var dealService = new Services.DealService(logger.Object, context, mapper.Object, mockUserAccessor.Object);
    
        await Assert.ThrowsAsync<NullReferenceException>(() => dealService.CreateDeal(deal, CancellationToken.None));
        
        mockUserAccessor.Verify(x => x.GetUsername());
        mockUserAccessor.VerifyNoOtherCalls();
        mockUserAccessor.VerifyAll();
        logger.VerifyNoOtherCalls();
        logger.VerifyAll();
    }
    
    [Theory]
    [InlineData(DealKind.Add, 500.00, 0, 500.00)]
    [InlineData(DealKind.Withdrawal, 477.00, 5.55, -482.55)]
    [InlineData(DealKind.Tax, 898.78, 1.22, -900.00)]
    [InlineData(DealKind.Dividends, 898.99, 5.56, 893.43)]
    [InlineData(DealKind.Add, 898.99, 5.56, 893.43)]
    public async Task CreateDeal_Success_Money(DealKind type, decimal price, decimal commission, decimal quantity)
    {
        var mockUserAccessor = new Mock<IUserAccessor>();
        var logger = new Mock<ILogger<Services.DealService>>();
        var context = SetupDbContext.Generate();
        var mapper = new Mapper(_mapperConfiguration);

        var currency = context.Currencies.First(x => x.Code == "RUB");
        
        var deal = new DealDto
        {
            Id = Guid.NewGuid(),
            AccountId = Guid.Parse("f1fe6744-86a6-4293-b469-64404511840f"),
            CreatedAt = DateTime.Now,
            Type = type,
            Price = price,
            Quantity = 1,
            Commission = commission,
            Currency = currency.Code
        };
        
        mockUserAccessor.Setup(x => x.GetUsername()).Returns("admin");
        
        var dealService = new Services.DealService(logger.Object, context, mapper, mockUserAccessor.Object);

        var actual = await dealService.CreateDeal(deal, CancellationToken.None);
        var deals = await context.Deals.ToListAsync();
        var assetActual1 = await context.Assets.FirstAsync(x => x.AccountId == deal.AccountId && x.StockId == deal.Currency);
        var actualDeal = await context.Deals.FirstAsync(x => x.Id == deal.Id);
        
        Assert.True(actual.IsSuccess);
        Assert.Equal(Unit.Value, actual.Result);
        Assert.Null(actual.Error);
        Assert.Single(deals);
        Assert.Equal(quantity, Math.Round(assetActual1.Quantity, 2));
        Assert.Equal((int)type, actualDeal.TypeId);
        Assert.Equal(price, actualDeal.Price);
        Assert.Equal(1, actualDeal.Quantity);
        Assert.Equal(commission, actualDeal.Commission);
        Assert.Equal(currency.Code, actualDeal.CurrencyId);
        Assert.Null(actualDeal.StockId);
        Assert.Equal(quantity, Math.Round(actualDeal.Amount, 2));
        mockUserAccessor.Verify(x => x.GetUsername());
        mockUserAccessor.VerifyNoOtherCalls();
        mockUserAccessor.VerifyAll();
        logger.VerifyNoOtherCalls();
        logger.VerifyAll();
    }
    
    [Theory]
    [InlineData(DealKind.Purchase, 236.54, 11, 56.98, 37341.08, 51.00, -2658.92)]
    [InlineData(DealKind.Sale, 236.54, 11, 43.71, 42558.23, 29.00, 2558.23)]
    public async Task CreateDeal_Success_Stock(DealKind type, decimal price, int quantity, decimal commission, decimal assetQuantity1, decimal assetQuantity2, decimal amount)
    {
        var mockUserAccessor = new Mock<IUserAccessor>();
        var logger = new Mock<ILogger<Services.DealService>>();
        var context = SetupDbContext.Generate();
        var mapper = new Mapper(_mapperConfiguration);
        
        var currency = context.Currencies.First(x => x.Code == "RUB");
        var stock2 = context.Stocks.First(x => x.Ticker == "MTSS");
        var asset1 = new Asset{AccountId = Guid.Parse("f1fe6744-86a6-4293-b469-64404511840f"), Id = Guid.NewGuid(), Quantity = 40000, StockId = currency.Code};
        var asset2 = new Asset{AccountId = Guid.Parse("f1fe6744-86a6-4293-b469-64404511840f"), Id = Guid.NewGuid(), Quantity = 40, StockId = stock2.Ticker};
        context.Assets.Add(asset1);
        context.Assets.Add(asset2);
        await context.SaveChangesAsync();

        var deal = new DealDto
        {
            Id = Guid.NewGuid(),
            AccountId = Guid.Parse("f1fe6744-86a6-4293-b469-64404511840f"),
            CreatedAt = DateTime.Now,
            Type = type,
            Price = price,
            Quantity = quantity,
            Commission = commission,
            Currency = currency.Code,
            Stock = stock2.Ticker
        };
        
        mockUserAccessor.Setup(x => x.GetUsername()).Returns("admin");
        
        var dealService = new Services.DealService(logger.Object, context, mapper, mockUserAccessor.Object);

        var actual = await dealService.CreateDeal(deal, CancellationToken.None);
        var deals = await context.Deals.ToListAsync();
        var assetActual1 = await context.Assets.FirstAsync(x => x.AccountId == deal.AccountId && x.StockId == currency.Code);
        var assetActual2 = await context.Assets.FirstAsync(x => x.AccountId == deal.AccountId && x.StockId == stock2.Ticker);
        var actualDeal = await context.Deals.FirstAsync(x => x.Id == deal.Id);
        
        Assert.True(actual.IsSuccess);
        Assert.Equal(Unit.Value, actual.Result);
        Assert.Null(actual.Error);
        Assert.Single(deals);
        Assert.Equal(assetQuantity1, assetActual1.Quantity);
        Assert.Equal(assetQuantity2, assetActual2.Quantity);
        Assert.Equal((int)type, actualDeal.TypeId);
        Assert.Equal(price, actualDeal.Price);
        Assert.Equal(quantity, actualDeal.Quantity);
        Assert.Equal(commission, actualDeal.Commission);
        Assert.Equal(stock2.Ticker, actualDeal.StockId);
        Assert.Equal(currency.Code, actualDeal.CurrencyId);
        Assert.Equal(amount, actualDeal.Amount);
        mockUserAccessor.Verify(x => x.GetUsername());
        mockUserAccessor.VerifyNoOtherCalls();
        mockUserAccessor.VerifyAll();
        logger.VerifyNoOtherCalls();
        logger.VerifyAll();
    }
}