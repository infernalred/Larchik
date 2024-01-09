using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using Larchik.Application.Contracts;
using Larchik.Application.Helpers;
using Larchik.Domain;
using Larchik.Domain.Enum;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Larchik.Application.Tests.DealService;

public class DeleteDealTests
{
    [Theory]
    [InlineData(48.53, 40, 40, 9, 8.53, 0.47, DealKind.Add, DealKind.Add, 40.00)]
    [InlineData(38.18, 49, 49, 9, -10.82, 1.82, DealKind.Add, DealKind.Withdrawal, 49.00)]
    [InlineData(55.99, 49, 49, 9, 6.99, 2.01, DealKind.Add, DealKind.Dividends, 49.00)]
    [InlineData(39.84, 49, 49, 9, -9.16, 0.16, DealKind.Add, DealKind.Tax, 49.00)]
    public async Task DeleteDeal_Success_Money_Variant1(decimal quantity, decimal price1, decimal amount1, decimal price2, decimal amount2, decimal commission, DealKind type1, DealKind type2, decimal expectQuantity)
    {
        var mockUserAccessor = new Mock<IUserAccessor>();
        var logger = new Mock<ILogger<Services.DealService>>();
        var context = SetupDbContext.Generate();
        var mapper = new Mock<IMapper>();
    
        var currency = context.Currencies.First(x => x.Code == "RUB");
        var user = await context.Users.FirstAsync(x => x.UserName == "admin");
        var asset1 = new Asset{AccountId = Guid.Parse("f1fe6744-86a6-4293-b469-64404511840f"), Id = Guid.NewGuid(), Quantity = quantity, StockId = currency.Code};
        
        var deal1 = new Deal
        {
            AccountId = asset1.AccountId,
            CreatedAt = new DateTime(2022, 05, 01), 
            Id = Guid.NewGuid(), 
            Quantity = 1, 
            Price = price1,
            Amount = amount1,
            TypeId = (int)type1, 
            CurrencyId = currency.Code,
            UserId = user.Id
        };
        
        var deal2 = new Deal
        {
            AccountId = asset1.AccountId,
            CreatedAt = new DateTime(2022, 05, 01), 
            Id = Guid.NewGuid(), 
            Quantity = 1, 
            Price = price2,
            Amount = amount2,
            Commission = commission,
            TypeId = (int)type2, 
            CurrencyId = currency.Code,
            UserId = user.Id
        };
        
        context.Assets.Add(asset1);
        context.Deals.AddRange(new List<Deal>{deal1, deal2});
        await context.SaveChangesAsync();
    
        var dealService = new Services.DealService(logger.Object, context, mapper.Object, mockUserAccessor.Object);
    
        var actual = await dealService.DeleteDeal(deal2.Id, CancellationToken.None);
    
        var deals = await context.Deals.ToListAsync();
        var asset = await context.Assets.FirstAsync(x => x.Id == asset1.Id);
    
        Assert.True(actual.IsSuccess);
        Assert.Equal(Unit.Value, actual.Value);
        Assert.Null(actual.Error);
        Assert.Single(deals);
        Assert.Equal(expectQuantity, asset.Quantity);
        mockUserAccessor.VerifyNoOtherCalls();
        mockUserAccessor.VerifyAll();
        logger.VerifyNoOtherCalls();
        logger.VerifyAll();
    }
    
    [Fact]
    public async Task DeleteDeal_Success_Stock_Variant1()
    {
        var mockUserAccessor = new Mock<IUserAccessor>();
        var logger = new Mock<ILogger<Services.DealService>>();
        var context = SetupDbContext.Generate();
        var mapper = new Mock<IMapper>();
    
        var currency = context.Currencies.First(x => x.Code == "RUB");
        var stock2 = context.Stock.First(x => x.Ticker == "MTSS");
        var user = await context.Users.FirstAsync(x => x.UserName == "admin");
        var asset1 = new Asset{AccountId = Guid.Parse("f1fe6744-86a6-4293-b469-64404511840f"), Id = Guid.NewGuid(), Quantity = 40000, StockId = currency.Code};
        var asset2 = new Asset{AccountId = Guid.Parse("f1fe6744-86a6-4293-b469-64404511840f"), Id = Guid.NewGuid(), Quantity = 40, StockId = stock2.Ticker};
        var deal1 = new Deal
        {
            AccountId = asset2.AccountId,
            CreatedAt = new DateTime(2022, 05, 01), 
            Id = Guid.NewGuid(), 
            Quantity = 20, 
            Price = 210,
            Amount = -4200,
            TypeId = (int)DealKind.Purchase,
            CurrencyId = currency.Code,
            StockId = stock2.Ticker,
            UserId = user.Id
        };
        var deal2 = new Deal
        {
            AccountId = asset2.AccountId,
            CreatedAt = new DateTime(2022, 05, 01), 
            Id = Guid.NewGuid(), 
            Quantity = 20, 
            Price = 220,
            Amount = -4403.44m,
            Commission = 3.44m,
            TypeId = (int)DealKind.Purchase,
            CurrencyId = currency.Code,
            StockId = stock2.Ticker,
            UserId = user.Id
        };
        
        context.Assets.Add(asset1);
        context.Assets.Add(asset2);
        context.Deals.AddRange(new List<Deal>{deal1, deal2});
        await context.SaveChangesAsync();
    
        var dealService = new Services.DealService(logger.Object, context, mapper.Object, mockUserAccessor.Object);
    
        var actual = await dealService.DeleteDeal(deal2.Id, CancellationToken.None);
    
        var deals = await context.Deals.ToListAsync();
        var assetActual1 = await context.Assets.FirstAsync(x => x.Id == asset1.Id);
        var assetActual2 = await context.Assets.FirstAsync(x => x.Id == asset2.Id);
    
        Assert.True(actual.IsSuccess);
        Assert.Equal(Unit.Value, actual.Value);
        Assert.Null(actual.Error);
        Assert.Single(deals);
        Assert.Equal(44403.44m, assetActual1.Quantity);
        Assert.Equal(20.00m, assetActual2.Quantity);
        mockUserAccessor.VerifyNoOtherCalls();
        mockUserAccessor.VerifyAll();
        logger.VerifyNoOtherCalls();
        logger.VerifyAll();
    }
    
    [Fact]
    public async Task DeleteDeal_Success_Stock_Variant2()
    {
        var mockUserAccessor = new Mock<IUserAccessor>();
        var logger = new Mock<ILogger<Services.DealService>>();
        var context = SetupDbContext.Generate();
        var mapper = new Mock<IMapper>();

        var currency = context.Currencies.First(x => x.Code == "RUB");
        var stock2 = context.Stock.First(x => x.Ticker == "MTSS");
        var user = await context.Users.FirstAsync(x => x.UserName == "admin");
        var asset1 = new Asset{AccountId = Guid.Parse("f1fe6744-86a6-4293-b469-64404511840f"), Id = Guid.NewGuid(), Quantity = 40000, StockId = currency.Code};
        var asset2 = new Asset{AccountId = Guid.Parse("f1fe6744-86a6-4293-b469-64404511840f"), Id = Guid.NewGuid(), Quantity = 20, StockId = stock2.Ticker};
        var deal1 = new Deal
        {
            AccountId = asset2.AccountId,
            CreatedAt = new DateTime(2022, 05, 01), 
            Id = Guid.NewGuid(), 
            Quantity = 20, 
            Price = 210,
            Amount = -4200,
            TypeId = (int)DealKind.Purchase,
            CurrencyId = currency.Code,
            StockId = stock2.Ticker,
            UserId = user.Id
        };
        var deal2 = new Deal
        {
            AccountId = asset2.AccountId,
            CreatedAt = new DateTime(2022, 05, 01), 
            Id = Guid.NewGuid(), 
            Quantity = 20, 
            Price = 220,
            Amount = 4397.11m,
            Commission = 2.89m,
            TypeId = (int)DealKind.Sale,
            CurrencyId = currency.Code,
            StockId = stock2.Ticker,
            UserId = user.Id
        };
        
        context.Assets.Add(asset1);
        context.Assets.Add(asset2);
        context.Deals.AddRange(new List<Deal>{deal1, deal2});
        await context.SaveChangesAsync();

        var dealService = new Services.DealService(logger.Object, context, mapper.Object, mockUserAccessor.Object);

        var actual = await dealService.DeleteDeal(deal2.Id, CancellationToken.None);

        var deals = await context.Deals.ToListAsync();
        var assetActual1 = await context.Assets.FirstAsync(x => x.Id == asset1.Id);
        var assetActual2 = await context.Assets.FirstAsync(x => x.Id == asset2.Id);

        Assert.True(actual.IsSuccess);
        Assert.Equal(Unit.Value, actual.Value);
        Assert.Null(actual.Error);
        Assert.Single(deals);
        Assert.Equal(35602.89m, assetActual1.Quantity);
        Assert.Equal(40.00m, assetActual2.Quantity);
        mockUserAccessor.VerifyNoOtherCalls();
        mockUserAccessor.VerifyAll();
        logger.VerifyNoOtherCalls();
        logger.VerifyAll();
    }
}