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
    public async Task EditDeal_Failure_DealIsNull()
    {
        var mockUserAccessor = new Mock<IUserAccessor>();
        var logger = new Mock<ILogger<Services.DealService>>();
        var context = SetupDbContext.Generate();
        var mapper = new Mock<IMapper>();
        
        var stock = context.Stocks.First(x => x.Ticker == "RUB");
        var asset1 = new Asset{AccountId = Guid.Parse("f1fe6744-86a6-4293-b469-64404511840f"), Id = Guid.NewGuid(), Quantity = 49, StockId = stock.Ticker};
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
            Amount = 9,
            OperationId = ListOperations.Add, 
            StockId = stock.Ticker
        };

        var deal = new DealDto
        {
            Id = Guid.NewGuid(),
            AccountId = Guid.NewGuid(),
            CreatedAt = DateTime.Now,
            Operation = ListOperations.Add,
            Price = 500,
            Quantity = 1,
            Stock = "RUB"
        };
        
        context.Assets.Add(asset1);
        context.Deals.AddRange(new List<Deal>{deal1, deal2});
        await context.SaveChangesAsync();
        
        var dealService = new Services.DealService(logger.Object, context, mapper.Object, mockUserAccessor.Object);

        var actual = await dealService.EditDeal(deal, CancellationToken.None);
        var deals = await context.Deals.ToListAsync();
        
        Assert.False(actual.IsSuccess);
        Assert.Equal(Unit.Value, actual.Result);
        Assert.NotNull(actual.Error);
        Assert.Equal("Сделка не найдена", actual.Error);
        Assert.Equal(2, deals.Count);
        mockUserAccessor.VerifyNoOtherCalls();
        mockUserAccessor.VerifyAll();
        logger.VerifyNoOtherCalls();
        logger.VerifyAll();
    }
    
    [Fact]
    public async Task EditDeal_Failure_AccountIsNull()
    {
        var mockUserAccessor = new Mock<IUserAccessor>();
        var logger = new Mock<ILogger<Services.DealService>>();
        var context = SetupDbContext.Generate();
        var mapper = new Mock<IMapper>();
        
        var stock = context.Stocks.First(x => x.Ticker == "RUB");
        var asset1 = new Asset{AccountId = Guid.Parse("f1fe6744-86a6-4293-b469-64404511840f"), Id = Guid.NewGuid(), Quantity = 49, StockId = stock.Ticker};
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
            Amount = 9,
            OperationId = ListOperations.Add, 
            StockId = stock.Ticker
        };

        var deal = new DealDto
        {
            Id = deal2.Id,
            AccountId = Guid.NewGuid(),
            CreatedAt = DateTime.Now,
            Operation = ListOperations.Add,
            Price = 500,
            Quantity = 1,
            Stock = "RUB"
        };
        
        context.Assets.Add(asset1);
        context.Deals.AddRange(new List<Deal>{deal1, deal2});
        await context.SaveChangesAsync();
        
        mockUserAccessor.Setup(x => x.GetUsername()).Returns("admin");
        
        var dealService = new Services.DealService(logger.Object, context, mapper.Object, mockUserAccessor.Object);

        var actual = await dealService.EditDeal(deal, CancellationToken.None);
        var deals = await context.Deals.ToListAsync();
        
        Assert.False(actual.IsSuccess);
        Assert.Equal(Unit.Value, actual.Result);
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
    public async Task EditDeal_Failure_StockIsNull()
    {
        var mockUserAccessor = new Mock<IUserAccessor>();
        var logger = new Mock<ILogger<Services.DealService>>();
        var context = SetupDbContext.Generate();
        var mapper = new Mock<IMapper>();
        
        var stock = context.Stocks.First(x => x.Ticker == "RUB");
        var asset1 = new Asset{AccountId = Guid.Parse("f1fe6744-86a6-4293-b469-64404511840f"), Id = Guid.NewGuid(), Quantity = 49, StockId = stock.Ticker};
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
            Amount = 9,
            OperationId = ListOperations.Add, 
            StockId = stock.Ticker
        };

        var deal = new DealDto
        {
            Id = deal2.Id,
            AccountId = asset1.AccountId,
            CreatedAt = DateTime.Now,
            Operation = ListOperations.Add,
            Price = 500,
            Quantity = 1,
            Stock = "RUB1"
        };
        
        context.Assets.Add(asset1);
        context.Deals.AddRange(new List<Deal>{deal1, deal2});
        await context.SaveChangesAsync();
        
        mockUserAccessor.Setup(x => x.GetUsername()).Returns("admin");
        
        var dealService = new Services.DealService(logger.Object, context, mapper.Object, mockUserAccessor.Object);

        var actual = await dealService.EditDeal(deal, CancellationToken.None);
        var deals = await context.Deals.ToListAsync();
        
        Assert.False(actual.IsSuccess);
        Assert.Equal(Unit.Value, actual.Result);
        Assert.NotNull(actual.Error);
        Assert.Equal("Тикер не найден", actual.Error);
        Assert.Equal(2, deals.Count);
        mockUserAccessor.Verify(x => x.GetUsername());
        mockUserAccessor.VerifyNoOtherCalls();
        mockUserAccessor.VerifyAll();
        logger.VerifyNoOtherCalls();
        logger.VerifyAll();
    }
    
    [Theory]
    [InlineData(ListOperations.Add, 500.00, 0.43, 499.57, 900.12)]
    [InlineData(ListOperations.Withdrawal, 269.33, 0.87, -270.20, 130.35)]
    [InlineData(ListOperations.Tax, 125.78, 0.93, -126.71, 273.84)]
    [InlineData(ListOperations.Dividends, 898.99, 0.23, 898.76, 1299.31)]
    public async Task EditDeal_Success_Money(string operation, double price, double commission, double amount, double quantity)
    {
        var mockUserAccessor = new Mock<IUserAccessor>();
        var logger = new Mock<ILogger<Services.DealService>>();
        var context = SetupDbContext.Generate();
        var mapper = new Mapper(_mapperConfiguration);

        var stock = context.Stocks.First(x => x.Ticker == "RUB");
        var asset1 = new Asset{AccountId = Guid.Parse("f1fe6744-86a6-4293-b469-64404511840f"), Id = Guid.NewGuid(), Quantity = 1400.54, StockId = stock.Ticker};
        var deal1 = new Deal
        {
            AccountId = asset1.AccountId,
            CreatedAt = new DateTime(2022, 05, 01), 
            Id = Guid.NewGuid(), 
            Quantity = 1, 
            Price = 400.55,
            Amount = 400.55,
            OperationId = ListOperations.Add, 
            StockId = stock.Ticker
        };
        var deal2 = new Deal
        {
            AccountId = asset1.AccountId,
            CreatedAt = new DateTime(2022, 05, 01), 
            Id = Guid.NewGuid(), 
            Quantity = 1, 
            Price = 999.99,
            Amount = 999.99,
            OperationId = ListOperations.Add, 
            StockId = stock.Ticker
        };

        var deal = new DealDto
        {
            Id = deal2.Id,
            AccountId = asset1.AccountId,
            CreatedAt = DateTime.Now,
            Operation = operation,
            Price = price,
            Quantity = 1,
            Commission = commission,
            Stock = stock.Ticker
        };
        
        context.Assets.Add(asset1);
        context.Deals.AddRange(new List<Deal>{deal1, deal2});
        await context.SaveChangesAsync();
        
        mockUserAccessor.Setup(x => x.GetUsername()).Returns("admin");
        
        var dealService = new Services.DealService(logger.Object, context, mapper, mockUserAccessor.Object);

        var actual = await dealService.EditDeal(deal, CancellationToken.None);
        var deals = await context.Deals.ToListAsync();
        var assetActual1 = await context.Assets.FirstAsync(x => x.AccountId == deal.AccountId && x.StockId == deal.Stock);
        var dealActual = deals.First(x => x.Id == deal2.Id);
        
        Assert.True(actual.IsSuccess);
        Assert.Equal(Unit.Value, actual.Result);
        Assert.Null(actual.Error);
        Assert.Equal(2, deals.Count);
        Assert.Equal(quantity, Math.Round(assetActual1.Quantity, 2));
        Assert.Equal(deal.CreatedAt, dealActual.CreatedAt);
        Assert.Equal(deal.Operation, dealActual.OperationId);
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
    [InlineData(ListOperations.Purchase, "SBER", 236.54, 11, 56.98, 41560.88, 20.00, -2658.92, 11.00)]
    [InlineData(ListOperations.Sale, "MTSS", 236.54, 11, 43.71, 46778.03, 20.00, 2558.23, 0.00)]
    public async Task EditDeal_Success_Stock(string operation, string ticker, double price, int quantity, double commission, double assetQuantity1, double assetQuantity2, double amount, double assetQuantity3)
    {
        var mockUserAccessor = new Mock<IUserAccessor>();
        var logger = new Mock<ILogger<Services.DealService>>();
        var context = SetupDbContext.Generate();
        var mapper = new Mapper(_mapperConfiguration);
        
        var stock1 = context.Stocks.First(x => x.Ticker == "RUB");
        var stock2 = context.Stocks.First(x => x.Ticker == "MTSS");
        var stock3 = context.Stocks.First(x => x.Ticker == "SBER");
        var asset1 = new Asset{AccountId = Guid.Parse("f1fe6744-86a6-4293-b469-64404511840f"), Id = Guid.NewGuid(), Quantity = 40000, StockId = stock1.Ticker};
        var asset2 = new Asset{AccountId = Guid.Parse("f1fe6744-86a6-4293-b469-64404511840f"), Id = Guid.NewGuid(), Quantity = 40, StockId = stock2.Ticker};
        var asset3 = new Asset{AccountId = Guid.Parse("f1fe6744-86a6-4293-b469-64404511840f"), Id = Guid.NewGuid(), Quantity = 0, StockId = stock3.Ticker};
        var deal1 = new Deal
        {
            AccountId = asset1.AccountId,
            CreatedAt = new DateTime(2022, 05, 01), 
            Id = Guid.NewGuid(), 
            Quantity = 20, 
            Price = 230.55,
            Amount = -4611.00,
            OperationId = ListOperations.Purchase, 
            StockId = stock2.Ticker
        };
        var deal2 = new Deal
        {
            AccountId = asset1.AccountId,
            CreatedAt = new DateTime(2022, 05, 01), 
            Id = Guid.NewGuid(), 
            Quantity = 20, 
            Price = 210.99,
            Amount = -4219.80,
            OperationId = ListOperations.Purchase, 
            StockId = stock2.Ticker
        };

        var deal = new DealDto
        {
            Id = deal2.Id,
            AccountId = asset1.AccountId,
            CreatedAt = DateTime.Now,
            Operation = operation,
            Price = price,
            Quantity = quantity,
            Commission = commission,
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
        var assetActual1 = await context.Assets.FirstAsync(x => x.AccountId == deal.AccountId && x.StockId == stock1.Ticker);
        var assetActual2 = await context.Assets.FirstAsync(x => x.AccountId == deal.AccountId && x.StockId == stock2.Ticker);
        var assetActual3 = await context.Assets.FirstAsync(x => x.AccountId == deal.AccountId && x.StockId == stock3.Ticker);
        var actualDeal = deals.First(x => x.Id == deal2.Id);
        
        Assert.True(actual.IsSuccess);
        Assert.Equal(Unit.Value, actual.Result);
        Assert.Null(actual.Error);
        Assert.Equal(2, deals.Count);
        Assert.Equal(assetQuantity1, Math.Round(assetActual1.Quantity, 2));
        Assert.Equal(assetQuantity2, Math.Round(assetActual2.Quantity, 2));
        Assert.Equal(assetQuantity3, Math.Round(assetActual3.Quantity, 2));
        Assert.Equal(deal.CreatedAt, actualDeal.CreatedAt);
        Assert.Equal(deal.Operation, actualDeal.OperationId);
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
}