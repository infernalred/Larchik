using System;
using System.Collections.Generic;
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

public class EditDealTests
{
    private readonly MapperConfiguration _mapperConfiguration = new(cfg => cfg.AddProfile(new MappingProfiles()));
    
    [Fact]
    public async Task EditDeal_Failure_AccountIsNull()
    {
        var mockUserAccessor = new Mock<IUserAccessor>();
        var logger = new Mock<ILogger<Services.DealService>>();
        var context = SetupDbContext.Generate();
        var mapper = new Mock<IMapper>();
        
        var currency = context.Currencies.First(x => x.Code == "RUB");
        var user = await context.Users.FirstAsync(x => x.UserName == "admin");
        var asset1 = new Asset{AccountId = Guid.Parse("f1fe6744-86a6-4293-b469-64404511840f"), Id = Guid.NewGuid(), Quantity = 49, StockId = currency.Code};
        
        var deal1 = new Deal
        {
            AccountId = asset1.AccountId,
            CreatedAt = new DateTime(2022, 05, 01), 
            Id = Guid.NewGuid(), 
            Quantity = 1, 
            Price = 40,
            Amount = 40,
            TypeId = (int)DealKind.Add, 
            CurrencyId = currency.Code,
            UserId = user.Id
        };
        
        var deal2 = new Deal
        {
            AccountId = asset1.AccountId,
            CreatedAt = new DateTime(2022, 05, 01), 
            Id = Guid.NewGuid(), 
            Quantity = 1, 
            Price = 9,
            Amount = 9,
            TypeId = (int)DealKind.Add, 
            CurrencyId = currency.Code,
            UserId = user.Id
        };

        var deal = new DealDto
        {
            Id = deal2.Id,
            AccountId = Guid.NewGuid(),
            CreatedAt = DateTime.Now,
            Type = DealKind.Add,
            Price = 500,
            Quantity = 1,
            Currency = currency.Code
        };
        
        context.Assets.Add(asset1);
        context.Deals.AddRange(new List<Deal>{deal1, deal2});
        await context.SaveChangesAsync();
        
        mockUserAccessor.Setup(x => x.GetUsername()).Returns("admin");
        
        var dealService = new Services.DealService(logger.Object, context, mapper.Object, mockUserAccessor.Object);

        var actual = await dealService.EditDeal(deal, CancellationToken.None);
        var deals = await context.Deals.ToListAsync();
        
        Assert.False(actual.IsSuccess);
        Assert.Equal(Unit.Value, actual.Value);
        Assert.NotNull(actual.Error);
        Assert.Equal("Счет не найден", actual.Error);
        Assert.Equal(2, deals.Count);
        mockUserAccessor.Verify(x => x.GetUsername());
        mockUserAccessor.VerifyNoOtherCalls();
        mockUserAccessor.VerifyAll();
        logger.VerifyNoOtherCalls();
        logger.VerifyAll();
    }
    
    [Fact]
    public async Task EditDeal_Failure_StockIsNotExist()
    {
        var mockUserAccessor = new Mock<IUserAccessor>();
        var logger = new Mock<ILogger<Services.DealService>>();
        var context = SetupDbContext.Generate();
        var mapper = new Mock<IMapper>();
        
        var currency = context.Currencies.First(x => x.Code == "RUB");
        var user = await context.Users.FirstAsync(x => x.UserName == "admin");
        var asset1 = new Asset{AccountId = Guid.Parse("f1fe6744-86a6-4293-b469-64404511840f"), Id = Guid.NewGuid(), Quantity = 49, StockId = currency.Code};
        
        var deal1 = new Deal
        {
            AccountId = asset1.AccountId,
            CreatedAt = new DateTime(2022, 05, 01), 
            Id = Guid.NewGuid(), 
            Quantity = 1, 
            Price = 40,
            Amount = 40,
            TypeId = (int)DealKind.Add,
            CurrencyId = currency.Code,
            UserId = user.Id
        };
        
        var deal2 = new Deal
        {
            AccountId = asset1.AccountId,
            CreatedAt = new DateTime(2022, 05, 01), 
            Id = Guid.NewGuid(), 
            Quantity = 1, 
            Price = 9,
            Amount = 9,
            TypeId = (int)DealKind.Add, 
            CurrencyId = currency.Code,
            UserId = user.Id
        };

        var deal = new DealDto
        {
            Id = deal2.Id,
            AccountId = asset1.AccountId,
            CreatedAt = DateTime.Now,
            Type = DealKind.Add,
            Price = 500,
            Quantity = 1,
            Currency = "RUB1"
        };
        
        context.Assets.Add(asset1);
        context.Deals.AddRange(new List<Deal>{deal1, deal2});
        await context.SaveChangesAsync();
        
        mockUserAccessor.Setup(x => x.GetUsername()).Returns("admin");
        
        var dealService = new Services.DealService(logger.Object, context, mapper.Object, mockUserAccessor.Object);

        await Assert.ThrowsAsync<DbUpdateException>(() => dealService.EditDeal(deal, CancellationToken.None));
        var deals = await context.Deals.ToListAsync();
        
        Assert.Equal(2, deals.Count);
        mockUserAccessor.Verify(x => x.GetUsername());
        mockUserAccessor.VerifyNoOtherCalls();
        mockUserAccessor.VerifyAll();
        logger.VerifyNoOtherCalls();
        logger.VerifyAll();
    }
    
    [Theory]
    [InlineData(DealKind.Add, 500.00, 0.43, 499.57, 900.12)]
    [InlineData(DealKind.Withdrawal, 269.33, 0.87, -270.20, 130.35)]
    [InlineData(DealKind.Tax, 125.78, 0.93, -126.71, 273.84)]
    [InlineData(DealKind.Dividends, 898.99, 0.23, 898.76, 1299.31)]
    public async Task EditDeal_Success_Money(DealKind type, decimal price, decimal commission, decimal amount, decimal quantity)
    {
        var mockUserAccessor = new Mock<IUserAccessor>();
        var logger = new Mock<ILogger<Services.DealService>>();
        var context = SetupDbContext.Generate();
        var mapper = new Mapper(_mapperConfiguration);

        var currency = context.Currencies.First(x => x.Code == "RUB");
        var user = await context.Users.FirstAsync(x => x.UserName == "admin");
        var asset1 = new Asset{AccountId = Guid.Parse("f1fe6744-86a6-4293-b469-64404511840f"), Id = Guid.NewGuid(), Quantity = 1400.54m, StockId = currency.Code};
        
        var deal1 = new Deal
        {
            AccountId = asset1.AccountId,
            CreatedAt = new DateTime(2022, 05, 01), 
            Id = Guid.NewGuid(), 
            Quantity = 1, 
            Price = 400.55m,
            Amount = 400.55m,
            TypeId = (int)DealKind.Add, 
            CurrencyId = currency.Code,
            UserId = user.Id
        };
        
        var deal2 = new Deal
        {
            AccountId = asset1.AccountId,
            CreatedAt = new DateTime(2022, 05, 01), 
            Id = Guid.NewGuid(), 
            Quantity = 1, 
            Price = 999.99m,
            Amount = 999.99m,
            TypeId = (int)DealKind.Add, 
            CurrencyId = currency.Code,
            UserId = user.Id
        };

        var deal = new DealDto
        {
            Id = deal2.Id,
            AccountId = asset1.AccountId,
            CreatedAt = DateTime.Now,
            Type = type,
            Price = price,
            Quantity = 1,
            Commission = commission,
            Currency = currency.Code
        };
        
        context.Assets.Add(asset1);
        context.Deals.AddRange(new List<Deal>{deal1, deal2});
        await context.SaveChangesAsync();
        
        mockUserAccessor.Setup(x => x.GetUsername()).Returns("admin");
        
        var dealService = new Services.DealService(logger.Object, context, mapper, mockUserAccessor.Object);

        var actual = await dealService.EditDeal(deal, CancellationToken.None);
        var deals = await context.Deals.ToListAsync();
        var assetActual1 = await context.Assets.FirstAsync(x => x.AccountId == deal.AccountId && x.StockId == deal.Currency);
        var dealActual = deals.First(x => x.Id == deal2.Id);
        
        Assert.True(actual.IsSuccess);
        Assert.Equal(Unit.Value, actual.Value);
        Assert.Null(actual.Error);
        Assert.Equal(2, deals.Count);
        Assert.Equal(quantity, assetActual1.Quantity);
        Assert.Equal(deal.CreatedAt, dealActual.CreatedAt);
        Assert.Equal((int)deal.Type, dealActual.TypeId);
        Assert.Equal(deal.Price, dealActual.Price);
        Assert.Equal(deal.Quantity, dealActual.Quantity);
        Assert.Equal(amount, dealActual.Amount);
        mockUserAccessor.Verify(x => x.GetUsername());
        mockUserAccessor.VerifyNoOtherCalls();
        mockUserAccessor.VerifyAll();
        logger.VerifyNoOtherCalls();
        logger.VerifyAll();
    }
    
    [Theory]
    [InlineData(DealKind.Purchase, "SBER", 236.54, 11, 56.98, 41560.88, 20.00, -2658.92, 11.00)]
    [InlineData(DealKind.Sale, "MTSS", 236.54, 11, 43.71, 46778.03, 9.00, 2558.23, 0.00)]
    public async Task EditDeal_Success_Stock(DealKind type, string ticker, decimal price, int quantity, decimal commission, decimal assetQuantity1, decimal assetQuantity2, decimal amount, decimal assetQuantity3)
    {
        var mockUserAccessor = new Mock<IUserAccessor>();
        var logger = new Mock<ILogger<Services.DealService>>();
        var context = SetupDbContext.Generate();
        var mapper = new Mapper(_mapperConfiguration);
        
        var currency = context.Currencies.First(x => x.Code == "RUB");
        var stock2 = context.Stock.First(x => x.Ticker == "MTSS");
        var stock3 = context.Stock.First(x => x.Ticker == "SBER");
        var user = await context.Users.FirstAsync(x => x.UserName == "admin");
        var asset1 = new Asset{AccountId = Guid.Parse("f1fe6744-86a6-4293-b469-64404511840f"), Id = Guid.NewGuid(), Quantity = 40000, StockId = currency.Code};
        var asset2 = new Asset{AccountId = Guid.Parse("f1fe6744-86a6-4293-b469-64404511840f"), Id = Guid.NewGuid(), Quantity = 40, StockId = stock2.Ticker};
        var asset3 = new Asset{AccountId = Guid.Parse("f1fe6744-86a6-4293-b469-64404511840f"), Id = Guid.NewGuid(), Quantity = 0, StockId = stock3.Ticker};
        
        var deal1 = new Deal
        {
            AccountId = asset1.AccountId,
            CreatedAt = new DateTime(2022, 05, 01), 
            Id = Guid.NewGuid(), 
            Quantity = 20, 
            Price = 230.55m,
            Amount = -4611.00m,
            TypeId = (int)DealKind.Purchase,
            CurrencyId = currency.Code,
            StockId = stock2.Ticker,
            UserId = user.Id
        };
        
        var deal2 = new Deal
        {
            AccountId = asset1.AccountId,
            CreatedAt = new DateTime(2022, 05, 01), 
            Id = Guid.NewGuid(), 
            Quantity = 20, 
            Price = 210.99m,
            Amount = -4219.80m,
            TypeId = (int)DealKind.Purchase,
            CurrencyId = currency.Code,
            StockId = stock2.Ticker,
            UserId = user.Id
        };

        var deal = new DealDto
        {
            Id = deal2.Id,
            AccountId = asset1.AccountId,
            CreatedAt = DateTime.Now,
            Type = type,
            Price = price,
            Quantity = quantity,
            Commission = commission,
            Currency = currency.Code,
            Stock = ticker
        };
        
        context.Deals.AddRange(new List<Deal>{deal1, deal2});
        context.Assets.Add(asset1);
        context.Assets.Add(asset2);
        context.Assets.Add(asset3);
        await context.SaveChangesAsync();
        
        mockUserAccessor.Setup(x => x.GetUsername()).Returns("admin");
        
        var dealService = new Services.DealService(logger.Object, context, mapper, mockUserAccessor.Object);

        var actual = await dealService.EditDeal(deal, CancellationToken.None);
        var deals = await context.Deals.ToListAsync();
        var assetActual1 = await context.Assets.FirstAsync(x => x.AccountId == deal.AccountId && x.StockId == currency.Code);
        var assetActual2 = await context.Assets.FirstAsync(x => x.AccountId == deal.AccountId && x.StockId == stock2.Ticker);
        var assetActual3 = await context.Assets.FirstAsync(x => x.AccountId == deal.AccountId && x.StockId == stock3.Ticker);
        var actualDeal = deals.First(x => x.Id == deal2.Id);
        
        Assert.True(actual.IsSuccess);
        Assert.Equal(Unit.Value, actual.Value);
        Assert.Null(actual.Error);
        Assert.Equal(2, deals.Count);
        Assert.Equal(assetQuantity1, assetActual1.Quantity);
        Assert.Equal(assetQuantity2, assetActual2.Quantity);
        Assert.Equal(assetQuantity3, assetActual3.Quantity, 2);
        Assert.Equal(deal.CreatedAt, actualDeal.CreatedAt);
        Assert.Equal((int)deal.Type, actualDeal.TypeId);
        Assert.Equal(deal.Price, actualDeal.Price);
        Assert.Equal(deal.Quantity, actualDeal.Quantity);
        Assert.Equal(deal.Commission, actualDeal.Commission);
        Assert.Equal(deal.Stock, actualDeal.StockId);
        Assert.Equal(amount, actualDeal.Amount);
        mockUserAccessor.Verify(x => x.GetUsername());
        mockUserAccessor.VerifyNoOtherCalls();
        mockUserAccessor.VerifyAll();
        logger.VerifyNoOtherCalls();
        logger.VerifyAll();
    }
    
    [Fact]
    public async Task EditDeal_Success_MoneyToStock()
    {
        var mockUserAccessor = new Mock<IUserAccessor>();
        var logger = new Mock<ILogger<Services.DealService>>();
        var context = SetupDbContext.Generate();
        var mapper = new Mapper(_mapperConfiguration);

        var currency = context.Currencies.First(x => x.Code == "RUB");
        var user = await context.Users.FirstAsync(x => x.UserName == "admin");
        var asset1 = new Asset{AccountId = Guid.Parse("f1fe6744-86a6-4293-b469-64404511840f"), Id = Guid.NewGuid(), Quantity = 1400.54m, StockId = currency.Code};
        
        var deal1 = new Deal
        {
            AccountId = asset1.AccountId,
            CreatedAt = new DateTime(2022, 05, 01), 
            Id = Guid.NewGuid(), 
            Quantity = 1, 
            Price = 400.55m,
            Amount = 400.55m,
            TypeId = (int)DealKind.Add, 
            CurrencyId = currency.Code,
            UserId = user.Id
        };
        
        var deal2 = new Deal
        {
            AccountId = asset1.AccountId,
            CreatedAt = new DateTime(2022, 05, 01), 
            Id = Guid.NewGuid(), 
            Quantity = 1, 
            Price = 999.99m,
            Amount = 999.99m,
            TypeId = (int)DealKind.Add, 
            CurrencyId = currency.Code,
            UserId = user.Id
        };

        var deal = new DealDto
        {
            Id = deal2.Id,
            AccountId = asset1.AccountId,
            CreatedAt = DateTime.Now,
            Type = DealKind.Purchase,
            Price = 16.78m,
            Quantity = 1,
            Commission = 0.78m,
            Currency = currency.Code,
            Stock = "MTSS"
        };
        
        context.Assets.Add(asset1);
        context.Deals.AddRange(new List<Deal>{deal1, deal2});
        await context.SaveChangesAsync();
        
        mockUserAccessor.Setup(x => x.GetUsername()).Returns("admin");
        
        var dealService = new Services.DealService(logger.Object, context, mapper, mockUserAccessor.Object);

        var actual = await dealService.EditDeal(deal, CancellationToken.None);
        var deals = await context.Deals.ToListAsync();
        var assetActual1 = await context.Assets.FirstAsync(x => x.AccountId == deal.AccountId && x.StockId == deal.Currency);
        var assetActual2 = await context.Assets.FirstAsync(x => x.AccountId == deal.AccountId && x.StockId == deal.Stock);
        var dealActual = deals.First(x => x.Id == deal2.Id);
        
        Assert.True(actual.IsSuccess);
        Assert.Equal(Unit.Value, actual.Value);
        Assert.Null(actual.Error);
        Assert.Equal(2, deals.Count);
        Assert.Equal(382.99m, assetActual1.Quantity);
        Assert.Equal(1, assetActual2.Quantity);
        Assert.Equal(deal.CreatedAt, dealActual.CreatedAt);
        Assert.Equal((int)deal.Type, dealActual.TypeId);
        Assert.Equal(deal.Price, dealActual.Price);
        Assert.Equal(deal.Quantity, dealActual.Quantity);
        Assert.Equal(-17.56m, dealActual.Amount);
        mockUserAccessor.Verify(x => x.GetUsername());
        mockUserAccessor.VerifyNoOtherCalls();
        mockUserAccessor.VerifyAll();
        logger.VerifyNoOtherCalls();
        logger.VerifyAll();
    }
}