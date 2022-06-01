using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Larchik.Application.Contracts;
using Larchik.Application.Helpers;
using Larchik.Application.Portfolios;
using Larchik.Domain;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Larchik.Application.Tests.Portfolio;

public class DetailsTests
{
    [Fact]
    public async Task Details_Variant_1()
    {
        var mockUserAccessor = new Mock<IUserAccessor>();
        var logger = new Mock<ILogger<Details.Handler>>();
        var context = SetupDbContext.Generate();
        
        var stock = context.Stocks.First(x => x.Ticker == "MTSS");
        var asset1 = new Asset{AccountId = Guid.Parse("f1fe6744-86a6-4293-b469-64404511840f"), Id = Guid.NewGuid(), Quantity = 49, StockId = stock.Ticker};
        var asset2 = new Asset{AccountId = Guid.Parse("860C80E3-AE30-4179-8E0E-C2C0DBA68268"), Id = Guid.NewGuid(), Quantity = 12, StockId = stock.Ticker};
        var deal1 = new Deal
        {
            AccountId = asset1.AccountId,
            CreatedAt = new DateTime(2022, 05, 01), 
            Id = Guid.NewGuid(), 
            Quantity = 50, 
            Price = 190,
            OperationId = ListOperations.Purchase, 
            StockId = stock.Ticker
        };
        var deal2 = new Deal
        {
            AccountId = asset2.AccountId,
            CreatedAt = new DateTime(2022, 05, 02), 
            Id = Guid.NewGuid(), 
            Quantity = 11, 
            Price = 230,
            OperationId = ListOperations.Purchase, 
            StockId = stock.Ticker
        };
        var deal3 = new Deal
        {
            AccountId = asset1.AccountId,
            CreatedAt = new DateTime(2022, 05, 03), 
            Id = Guid.NewGuid(), 
            Quantity = 5, 
            Price = 210,
            OperationId = ListOperations.Purchase, 
            StockId = stock.Ticker
        };
        var deal4 = new Deal
        {
            AccountId = asset1.AccountId,
            CreatedAt = new DateTime(2022, 05, 04), 
            Id = Guid.NewGuid(), 
            Quantity = 3, 
            Price = 100,
            OperationId = ListOperations.Sale, 
            StockId = stock.Ticker
        };
        var deal5 = new Deal
        {
            AccountId = asset1.AccountId,
            CreatedAt = new DateTime(2022, 05, 05), 
            Id = Guid.NewGuid(), 
            Quantity = 3, 
            Price = 100,
            OperationId = ListOperations.Sale, 
            StockId = stock.Ticker
        };
        var deal6 = new Deal
        {
            AccountId = asset2.AccountId,
            CreatedAt = new DateTime(2022, 05, 06), 
            Id = Guid.NewGuid(), 
            Quantity = 1, 
            Price = 55,
            OperationId = ListOperations.Purchase, 
            StockId = stock.Ticker
        };
        context.Assets.Add(asset1);
        context.Assets.Add(asset2);
        context.Deals.AddRange(new List<Deal>{deal1, deal2, deal3, deal4, deal5, deal6});
        await context.SaveChangesAsync();
        
        mockUserAccessor.Setup(x => x.GetUsername()).Returns("admin");
    
        var handler = new Details.Handler(logger.Object, context, mockUserAccessor.Object);
    
        var actual = await handler.Handle(new Details.Query(), CancellationToken.None);
    
        Assert.True(actual.IsSuccess);
        Assert.Single(actual.Result.Assets);
        Assert.Equal(196.64, Math.Round(actual.Result.Assets[0].AveragePrice, 2));
        Assert.Equal(stock.LastPrice, actual.Result.Assets[0].Price);
        Assert.Equal(stock.TypeId, actual.Result.Assets[0].Type);
        Assert.Equal(stock.SectorId, actual.Result.Assets[0].Sector);
        Assert.Equal(stock.Ticker, actual.Result.Assets[0].Ticker);
        Assert.Equal(stock.CompanyName, actual.Result.Assets[0].CompanyName);
        Assert.Equal(61, actual.Result.Assets[0].Quantity);
        mockUserAccessor.Verify(x => x.GetUsername());
        mockUserAccessor.VerifyNoOtherCalls();
        mockUserAccessor.VerifyAll();
        logger.VerifyNoOtherCalls();
        logger.VerifyAll();
    }

    [Theory]
    [MemberData(nameof(Data))]
    public async Task Details_Success(
        string ticker, 
        double quantity, 
        double averagePrice, 
        Tuple<int, double, string> data1, 
        Tuple<int, double, string> data2, 
        Tuple<int, double, string> data3, 
        Tuple<int, double, string> data4)
    {
        var mockUserAccessor = new Mock<IUserAccessor>();
        var logger = new Mock<ILogger<Details.Handler>>();
        var context = SetupDbContext.Generate();

        var stock = context.Stocks.First(x => x.Ticker == ticker);
        var asset1 = new Asset{AccountId = Guid.Parse("f1fe6744-86a6-4293-b469-64404511840f"), Id = Guid.NewGuid(), Quantity = quantity, StockId = ticker};
        var deal1 = new Deal
        {
            AccountId = asset1.AccountId,
            CreatedAt = new DateTime(2022, 05, 01), 
            Id = Guid.NewGuid(), 
            Quantity = data1.Item1, 
            Price = data1.Item2,
            OperationId = data1.Item3, 
            StockId = stock.Ticker
        };
        var deal2 = new Deal
        {
            AccountId = asset1.AccountId,
            CreatedAt = new DateTime(2022, 05, 02), 
            Id = Guid.NewGuid(), 
            Quantity = data2.Item1, 
            Price = data2.Item2,
            OperationId = data2.Item3,  
            StockId = stock.Ticker
        };
        var deal3 = new Deal
        {
            AccountId = asset1.AccountId,
            CreatedAt = new DateTime(2022, 05, 03), 
            Id = Guid.NewGuid(), 
            Quantity = data3.Item1, 
            Price = data3.Item2,
            OperationId = data3.Item3,  
            StockId = stock.Ticker
        };
        var deal4 = new Deal
        {
            AccountId = asset1.AccountId,
            CreatedAt = new DateTime(2022, 05, 04), 
            Id = Guid.NewGuid(), 
            Quantity = data4.Item1, 
            Price = data4.Item2,
            OperationId = data4.Item3,
            StockId = stock.Ticker
        };
        context.Assets.Add(asset1);
        context.Deals.AddRange(new List<Deal>{deal1, deal2, deal3, deal4});
        await context.SaveChangesAsync();
        
        mockUserAccessor.Setup(x => x.GetUsername()).Returns("admin");

        var handler = new Details.Handler(logger.Object, context, mockUserAccessor.Object);

        var actual = await handler.Handle(new Details.Query(), CancellationToken.None);

        Assert.True(actual.IsSuccess);
        Assert.Single(actual.Result.Assets);
        Assert.Equal(averagePrice, Math.Round(actual.Result.Assets[0].AveragePrice, 2));
        Assert.Equal(stock.LastPrice, actual.Result.Assets[0].Price);
        Assert.Equal(stock.TypeId, actual.Result.Assets[0].Type);
        Assert.Equal(stock.SectorId, actual.Result.Assets[0].Sector);
        Assert.Equal(stock.Ticker, actual.Result.Assets[0].Ticker);
        Assert.Equal(stock.CompanyName, actual.Result.Assets[0].CompanyName);
        Assert.Equal(quantity, actual.Result.Assets[0].Quantity);
        mockUserAccessor.Verify(x => x.GetUsername());
        mockUserAccessor.VerifyNoOtherCalls();
        mockUserAccessor.VerifyAll();
        logger.VerifyNoOtherCalls();
        logger.VerifyAll();
    }
    
    public static IEnumerable<object[]> Data(){
        yield return new object[] { "USD", 556.32, 75.00, Tuple.Create(1, 356.32, ListOperations.Add), Tuple.Create(445, 75.00, ListOperations.Purchase), Tuple.Create(345, 56.89, ListOperations.Sale), Tuple.Create(1, 300.00, ListOperations.Withdrawal)};
        yield return new object[] { "RUB", 16389.01, 1.00, Tuple.Create(1, 6300.56, ListOperations.Add), Tuple.Create(1, 8579.06, ListOperations.Add), Tuple.Create(1, 7369.00, ListOperations.Add), Tuple.Create(1, 5859.61, ListOperations.Add)};
        yield return new object[] { "MTSS", 40, 190.00, Tuple.Create(3, 243.00, ListOperations.Purchase), Tuple.Create(3, 179.00, ListOperations.Sale), Tuple.Create(41, 190.00, ListOperations.Purchase), Tuple.Create(1, 163.00, ListOperations.Sale)};
        yield return new object[] { "MTSS", 8, 177.62, Tuple.Create(3, 250.00, ListOperations.Purchase), Tuple.Create(3, 210.00, ListOperations.Purchase), Tuple.Create(5, 220.00, ListOperations.Sale), Tuple.Create(7, 173.00, ListOperations.Purchase)};
        yield return new object[] { "MTSS", 12, 196.67, Tuple.Create(3, 250.00, ListOperations.Purchase), Tuple.Create(3, 210.00, ListOperations.Sale), Tuple.Create(5, 220.00, ListOperations.Purchase), Tuple.Create(7, 180.00, ListOperations.Purchase)};
    }
}