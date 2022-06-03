using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using Larchik.Application.Contracts;
using Larchik.Application.Helpers;
using Larchik.Domain;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Larchik.Application.Tests.DealService;

public class DeleteDealTests
{
    [Fact]
    public async Task DeleteDeal_Fail_Variant1()
    {
        var mockUserAccessor = new Mock<IUserAccessor>();
        var logger = new Mock<ILogger<Services.DealService>>();
        var context = SetupDbContext.Generate();
        var mapper = new Mock<IMapper>();
    
        var dealService = new Services.DealService(logger.Object, context, mapper.Object, mockUserAccessor.Object);
    
        var actual = await dealService.DeleteDeal(Guid.NewGuid(), CancellationToken.None);
    
        Assert.False(actual.IsSuccess);
        Assert.Equal(Unit.Value, actual.Result);
        Assert.NotNull(actual.Error);
        mockUserAccessor.VerifyNoOtherCalls();
        mockUserAccessor.VerifyAll();
        logger.VerifyNoOtherCalls();
        logger.VerifyAll();
    }
    
    [Fact]
    public async Task DeleteDeal_Fail_Variant2()
    {
        var mockUserAccessor = new Mock<IUserAccessor>();
        var logger = new Mock<ILogger<Services.DealService>>();
        var context = SetupDbContext.Generate();
        var mapper = new Mock<IMapper>();
    
        var deal = new Deal {Id = Guid.NewGuid() };
        context.Deals.Add(deal);
        await context.SaveChangesAsync();
    
        var dealService = new Services.DealService(logger.Object, context, mapper.Object, mockUserAccessor.Object);
    
        var actual = await dealService.DeleteDeal(Guid.NewGuid(), CancellationToken.None);
    
        var deals = await context.Deals.ToListAsync();
    
        Assert.False(actual.IsSuccess);
        Assert.Equal(Unit.Value, actual.Result);
        Assert.NotNull(actual.Error);
        Assert.Single(deals);
        mockUserAccessor.VerifyNoOtherCalls();
        mockUserAccessor.VerifyAll();
        logger.VerifyNoOtherCalls();
        logger.VerifyAll();
    }
    
    [Fact]
    public async Task DeleteDeal_Success_Money_Variant1()
    {
        var mockUserAccessor = new Mock<IUserAccessor>();
        var logger = new Mock<ILogger<Services.DealService>>();
        var context = SetupDbContext.Generate();
        var mapper = new Mock<IMapper>();
    
        var stock = context.Stocks.First(x => x.Ticker == "RUB");
        var asset1 = new Asset{AccountId = Guid.Parse("f1fe6744-86a6-4293-b469-64404511840f"), Id = Guid.NewGuid(), Quantity = 48.53m, StockId = stock.Ticker};
        var deal1 = new Deal
        {
            AccountId = asset1.AccountId,
            CreatedAt = new DateTime(2022, 05, 01), 
            Id = Guid.NewGuid(), 
            Quantity = 1, 
            Price = 40,
            Amount = 40,
            OperationId = ListOperations.Add, 
            StockId = stock.Ticker
        };
        var deal2 = new Deal
        {
            AccountId = asset1.AccountId,
            CreatedAt = new DateTime(2022, 05, 01), 
            Id = Guid.NewGuid(), 
            Quantity = 1, 
            Price = 9,
            Amount = 8.53m,
            Commission = 0.47m,
            OperationId = ListOperations.Add, 
            StockId = stock.Ticker
        };
        
        context.Assets.Add(asset1);
        context.Deals.AddRange(new List<Deal>{deal1, deal2});
        await context.SaveChangesAsync();
    
        var dealService = new Services.DealService(logger.Object, context, mapper.Object, mockUserAccessor.Object);
    
        var actual = await dealService.DeleteDeal(deal2.Id, CancellationToken.None);
    
        var deals = await context.Deals.ToListAsync();
        var asset = await context.Assets.FirstAsync(x => x.Id == asset1.Id);
    
        Assert.True(actual.IsSuccess);
        Assert.Equal(Unit.Value, actual.Result);
        Assert.Null(actual.Error);
        Assert.Single(deals);
        Assert.Equal(40.00m, asset.Quantity);
        mockUserAccessor.VerifyNoOtherCalls();
        mockUserAccessor.VerifyAll();
        logger.VerifyNoOtherCalls();
        logger.VerifyAll();
    }
    
    [Fact]
    public async Task DeleteDeal_Success_Money_Variant2()
    {
        var mockUserAccessor = new Mock<IUserAccessor>();
        var logger = new Mock<ILogger<Services.DealService>>();
        var context = SetupDbContext.Generate();
        var mapper = new Mock<IMapper>();
    
        var stock = context.Stocks.First(x => x.Ticker == "RUB");
        var asset1 = new Asset{AccountId = Guid.Parse("f1fe6744-86a6-4293-b469-64404511840f"), Id = Guid.NewGuid(), Quantity = 38.18m, StockId = stock.Ticker};
        var deal1 = new Deal
        {
            AccountId = asset1.AccountId,
            CreatedAt = new DateTime(2022, 05, 01), 
            Id = Guid.NewGuid(), 
            Quantity = 1, 
            Price = 49,
            Amount = 49,
            OperationId = ListOperations.Add, 
            StockId = stock.Ticker
        };
        var deal2 = new Deal
        {
            AccountId = asset1.AccountId,
            CreatedAt = new DateTime(2022, 05, 01), 
            Id = Guid.NewGuid(), 
            Quantity = 1, 
            Price = 9,
            Amount = -10.82m,
            Commission = 1.82m,
            OperationId = ListOperations.Withdrawal, 
            StockId = stock.Ticker
        };
        
        context.Assets.Add(asset1);
        context.Deals.AddRange(new List<Deal>{deal1, deal2});
        await context.SaveChangesAsync();
        
        var dealService = new Services.DealService(logger.Object, context, mapper.Object, mockUserAccessor.Object);
        
        var actual = await dealService.DeleteDeal(deal2.Id, CancellationToken.None);
        
        var deals = await context.Deals.ToListAsync();
        var asset = await context.Assets.FirstAsync(x => x.Id == asset1.Id);
    
        Assert.True(actual.IsSuccess);
        Assert.Equal(Unit.Value, actual.Result);
        Assert.Null(actual.Error);
        Assert.Single(deals);
        Assert.Equal(49.00m, asset.Quantity);
        mockUserAccessor.VerifyNoOtherCalls();
        mockUserAccessor.VerifyAll();
        logger.VerifyNoOtherCalls();
        logger.VerifyAll();
    }
    
    [Fact]
    public async Task DeleteDeal_Success_Money_Variant3()
    {
        var mockUserAccessor = new Mock<IUserAccessor>();
        var logger = new Mock<ILogger<Services.DealService>>();
        var context = SetupDbContext.Generate();
        var mapper = new Mock<IMapper>();
    
        var stock = context.Stocks.First(x => x.Ticker == "RUB");
        var asset1 = new Asset{AccountId = Guid.Parse("f1fe6744-86a6-4293-b469-64404511840f"), Id = Guid.NewGuid(), Quantity = 55.99m, StockId = stock.Ticker};
        var deal1 = new Deal
        {
            AccountId = asset1.AccountId,
            CreatedAt = new DateTime(2022, 05, 01), 
            Id = Guid.NewGuid(), 
            Quantity = 1, 
            Price = 49,
            Amount = 49,
            OperationId = ListOperations.Add, 
            StockId = stock.Ticker
        };
        var deal2 = new Deal
        {
            AccountId = asset1.AccountId,
            CreatedAt = new DateTime(2022, 05, 01), 
            Id = Guid.NewGuid(), 
            Quantity = 1, 
            Price = 9,
            Amount = 6.99m,
            Commission = 2.01m,
            OperationId = ListOperations.Dividends, 
            StockId = stock.Ticker
        };
        
        context.Assets.Add(asset1);
        context.Deals.AddRange(new List<Deal>{deal1, deal2});
        await context.SaveChangesAsync();
    
        var dealService = new Services.DealService(logger.Object, context, mapper.Object, mockUserAccessor.Object);
    
        var actual = await dealService.DeleteDeal(deal2.Id, CancellationToken.None);
    
        var deals = await context.Deals.ToListAsync();
        var asset = await context.Assets.FirstAsync(x => x.Id == asset1.Id);
    
        Assert.True(actual.IsSuccess);
        Assert.Equal(Unit.Value, actual.Result);
        Assert.Null(actual.Error);
        Assert.Single(deals);
        Assert.Equal(49.00m, asset.Quantity);
        mockUserAccessor.VerifyNoOtherCalls();
        mockUserAccessor.VerifyAll();
        logger.VerifyNoOtherCalls();
        logger.VerifyAll();
    }
    
    [Fact]
    public async Task DeleteDeal_Success_Money_Variant4()
    {
        var mockUserAccessor = new Mock<IUserAccessor>();
        var logger = new Mock<ILogger<Services.DealService>>();
        var context = SetupDbContext.Generate();
        var mapper = new Mock<IMapper>();
    
        var stock = context.Stocks.First(x => x.Ticker == "RUB");
        var asset1 = new Asset{AccountId = Guid.Parse("f1fe6744-86a6-4293-b469-64404511840f"), Id = Guid.NewGuid(), Quantity = 39.84m, StockId = stock.Ticker};
        var deal1 = new Deal
        {
            AccountId = asset1.AccountId,
            CreatedAt = new DateTime(2022, 05, 01), 
            Id = Guid.NewGuid(), 
            Quantity = 1, 
            Price = 49,
            Amount = 49,
            OperationId = ListOperations.Add, 
            StockId = stock.Ticker
        };
        var deal2 = new Deal
        {
            AccountId = asset1.AccountId,
            CreatedAt = new DateTime(2022, 05, 01), 
            Id = Guid.NewGuid(), 
            Quantity = 1, 
            Price = 9,
            Amount = -9.16m,
            Commission = 0.16m,
            OperationId = ListOperations.Tax, 
            StockId = stock.Ticker
        };
        
        context.Assets.Add(asset1);
        context.Deals.AddRange(new List<Deal>{deal1, deal2});
        await context.SaveChangesAsync();
    
        var dealService = new Services.DealService(logger.Object, context, mapper.Object, mockUserAccessor.Object);
    
        var actual = await dealService.DeleteDeal(deal2.Id, CancellationToken.None);
    
        var deals = await context.Deals.ToListAsync();
        var asset = await context.Assets.FirstAsync(x => x.Id == asset1.Id);
    
        Assert.True(actual.IsSuccess);
        Assert.Equal(Unit.Value, actual.Result);
        Assert.Null(actual.Error);
        Assert.Single(deals);
        Assert.Equal(49.00m, asset.Quantity);
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
    
        var stock = context.Stocks.First(x => x.Ticker == "RUB");
        var stock2 = context.Stocks.First(x => x.Ticker == "MTSS");
        var asset1 = new Asset{AccountId = Guid.Parse("f1fe6744-86a6-4293-b469-64404511840f"), Id = Guid.NewGuid(), Quantity = 40000, StockId = stock.Ticker};
        var asset2 = new Asset{AccountId = Guid.Parse("f1fe6744-86a6-4293-b469-64404511840f"), Id = Guid.NewGuid(), Quantity = 40, StockId = stock2.Ticker};
        var deal1 = new Deal
        {
            AccountId = asset2.AccountId,
            CreatedAt = new DateTime(2022, 05, 01), 
            Id = Guid.NewGuid(), 
            Quantity = 20, 
            Price = 210,
            Amount = -4200,
            OperationId = ListOperations.Purchase, 
            StockId = stock2.Ticker
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
            OperationId = ListOperations.Purchase, 
            StockId = stock2.Ticker
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
        Assert.Equal(Unit.Value, actual.Result);
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

        var stock = context.Stocks.First(x => x.Ticker == "RUB");
        var stock2 = context.Stocks.First(x => x.Ticker == "MTSS");
        var asset1 = new Asset{AccountId = Guid.Parse("f1fe6744-86a6-4293-b469-64404511840f"), Id = Guid.NewGuid(), Quantity = 40000, StockId = stock.Ticker};
        var asset2 = new Asset{AccountId = Guid.Parse("f1fe6744-86a6-4293-b469-64404511840f"), Id = Guid.NewGuid(), Quantity = 20, StockId = stock2.Ticker};
        var deal1 = new Deal
        {
            AccountId = asset2.AccountId,
            CreatedAt = new DateTime(2022, 05, 01), 
            Id = Guid.NewGuid(), 
            Quantity = 20, 
            Price = 210,
            Amount = -4200,
            OperationId = ListOperations.Purchase, 
            StockId = stock2.Ticker
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
            OperationId = ListOperations.Sale, 
            StockId = stock2.Ticker
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
        Assert.Equal(Unit.Value, actual.Result);
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