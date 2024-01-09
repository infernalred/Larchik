// using System;
// using System.Collections.Generic;
// using System.Linq;
// using System.Threading;
// using System.Threading.Tasks;
// using AutoMapper;
// using Larchik.Application.Contracts;
// using Larchik.Application.Helpers;
// using Larchik.Application.Services.Contracts;
// using Larchik.Domain;
// using Larchik.Domain.Enum;
// using Microsoft.EntityFrameworkCore;
// using Microsoft.Extensions.Logging;
// using Moq;
// using Xunit;
//
// namespace Larchik.Application.Tests.PortfolioService;
//
// public class PortfolioServiceTests
// {
//     private readonly MapperConfiguration _mapperConfiguration = new(cfg => cfg.AddProfile(new MappingProfiles()));
//     
//     [Fact]
//     public async Task GetPortfolioAsync_Variant1()
//     {
//         var mockUserAccessor = new Mock<IUserAccessor>();
//         var logger = new Mock<ILogger<Services.PortfolioService>>();
//         var exchange = new Mock<IExchangeService>();
//         var context = SetupDbContext.Generate();
//         var mapper = new Mapper(_mapperConfiguration);
//         
//         var user = await context.Users.FirstAsync(x => x.UserName == "admin");
//         var test = await context.Users.FirstAsync(x => x.UserName == "test");
//         var currency = context.Currencies.First(x => x.Code == "RUB");
//         var stock = context.Stocks.First(x => x.Ticker == "MTSS");
//         var asset1 = new Asset{AccountId = Guid.Parse("f1fe6744-86a6-4293-b469-64404511840f"), Id = Guid.NewGuid(), Quantity = 49, StockId = stock.Ticker};
//         var asset2 = new Asset{AccountId = Guid.Parse("860C80E3-AE30-4179-8E0E-C2C0DBA68268"), Id = Guid.NewGuid(), Quantity = 12, StockId = stock.Ticker};
//         var asset3 = new Asset{AccountId = Guid.Parse("a4c15931-70ca-4a55-9fac-0715c4a56264"), Id = Guid.NewGuid(), Quantity = 12, StockId = stock.Ticker};
//         
//         var deal1 = new Deal
//         {
//             AccountId = asset1.AccountId,
//             CreatedAt = new DateTime(2022, 05, 01), 
//             Id = Guid.NewGuid(), 
//             Quantity = 50, 
//             Price = 190,
//             TypeId = (int)DealKind.Purchase, 
//             StockId = stock.Ticker,
//             CurrencyId = currency.Code,
//             UserId = user.Id
//         };
//         
//         var deal2 = new Deal
//         {
//             AccountId = asset2.AccountId,
//             CreatedAt = new DateTime(2022, 05, 02), 
//             Id = Guid.NewGuid(), 
//             Quantity = 11, 
//             Price = 230,
//             TypeId = (int)DealKind.Purchase, 
//             StockId = stock.Ticker,
//             CurrencyId = currency.Code,
//             UserId = user.Id
//         };
//         
//         var deal3 = new Deal
//         {
//             AccountId = asset1.AccountId,
//             CreatedAt = new DateTime(2022, 05, 03), 
//             Id = Guid.NewGuid(), 
//             Quantity = 5, 
//             Price = 210,
//             TypeId = (int)DealKind.Purchase, 
//             StockId = stock.Ticker,
//             CurrencyId = currency.Code,
//             UserId = user.Id
//         };
//         
//         var deal4 = new Deal
//         {
//             AccountId = asset1.AccountId,
//             CreatedAt = new DateTime(2022, 05, 04), 
//             Id = Guid.NewGuid(), 
//             Quantity = 3, 
//             Price = 100,
//             TypeId = (int)DealKind.Sale, 
//             StockId = stock.Ticker,
//             CurrencyId = currency.Code,
//             UserId = user.Id
//         };
//         
//         var deal5 = new Deal
//         {
//             AccountId = asset1.AccountId,
//             CreatedAt = new DateTime(2022, 05, 05), 
//             Id = Guid.NewGuid(), 
//             Quantity = 3, 
//             Price = 100,
//             TypeId = (int)DealKind.Sale, 
//             StockId = stock.Ticker,
//             CurrencyId = currency.Code,
//             UserId = user.Id
//         };
//         
//         var deal6 = new Deal
//         {
//             AccountId = asset2.AccountId,
//             CreatedAt = new DateTime(2022, 05, 06), 
//             Id = Guid.NewGuid(), 
//             Quantity = 1, 
//             Price = 55,
//             TypeId = (int)DealKind.Purchase, 
//             StockId = stock.Ticker,
//             CurrencyId = currency.Code,
//             UserId = user.Id
//         };
//         
//         var deal7 = new Deal
//         {
//             AccountId = asset3.AccountId,
//             CreatedAt = new DateTime(2022, 05, 03), 
//             Id = Guid.NewGuid(), 
//             Quantity = 5, 
//             Price = 210,
//             TypeId = (int)DealKind.Purchase, 
//             StockId = stock.Ticker,
//             CurrencyId = currency.Code,
//             UserId = test.Id
//         };
//         
//         var deal8 = new Deal
//         {
//             AccountId = asset3.AccountId,
//             CreatedAt = new DateTime(2022, 05, 04), 
//             Id = Guid.NewGuid(), 
//             Quantity = 3, 
//             Price = 100,
//             TypeId = (int)DealKind.Sale, 
//             StockId = stock.Ticker,
//             CurrencyId = currency.Code,
//             UserId = test.Id
//         };
//         
//         context.Assets.Add(asset1);
//         context.Assets.Add(asset2);
//         context.Assets.Add(asset3);
//         context.Deals.AddRange(new List<Deal>{deal1, deal2, deal3, deal4, deal5, deal6, deal7, deal8});
//         await context.SaveChangesAsync();
//         
//         mockUserAccessor.Setup(x => x.GetUsername()).Returns("admin");
//     
//         var service = new Services.PortfolioService(logger.Object, context, mockUserAccessor.Object, exchange.Object, mapper);
//     
//         var actual = await service.GetPortfolioAsync(CancellationToken.None);
//     
//         Assert.Single(actual.Assets);
//         Assert.Equal(196.64m, Math.Round(actual.Assets[0].AveragePrice, 2));
//         Assert.Equal(new decimal(stock.LastPrice), (decimal)actual.Assets[0].Stock.LastPrice);
//         Assert.Equal(stock.Type, actual.Assets[0].Stock.Type);
//         Assert.Equal(stock.SectorId, actual.Assets[0].Stock.Sector);
//         Assert.Equal(stock.Ticker, actual.Assets[0].Stock.Ticker);
//         Assert.Equal(stock.CompanyName, actual.Assets[0].Stock.CompanyName);
//         Assert.Equal(61, actual.Assets[0].Quantity);
//         mockUserAccessor.Verify(x => x.GetUsername());
//         mockUserAccessor.VerifyNoOtherCalls();
//         mockUserAccessor.VerifyAll();
//         logger.VerifyNoOtherCalls();
//         logger.VerifyAll();
//     }
//     
//     [Fact]
//     public async Task GetPortfolioAsync_Id()
//     {
//         var mockUserAccessor = new Mock<IUserAccessor>();
//         var logger = new Mock<ILogger<Services.PortfolioService>>();
//         var exchange = new Mock<IExchangeService>();
//         var context = SetupDbContext.Generate();
//         var mapper = new Mapper(_mapperConfiguration);
//         
//         var user = await context.Users.FirstAsync(x => x.UserName == "admin");
//         var test = await context.Users.FirstAsync(x => x.UserName == "test");
//         var currency = context.Currencies.First(x => x.Code == "RUB");
//         var stock = context.Stocks.First(x => x.Ticker == "MTSS");
//         var asset1 = new Asset{AccountId = Guid.Parse("f1fe6744-86a6-4293-b469-64404511840f"), Id = Guid.NewGuid(), Quantity = 49, StockId = stock.Ticker};
//         var asset2 = new Asset{AccountId = Guid.Parse("860C80E3-AE30-4179-8E0E-C2C0DBA68268"), Id = Guid.NewGuid(), Quantity = 12, StockId = stock.Ticker};
//         var asset3 = new Asset{AccountId = Guid.Parse("a4c15931-70ca-4a55-9fac-0715c4a56264"), Id = Guid.NewGuid(), Quantity = 12, StockId = stock.Ticker};
//         
//         var deal1 = new Deal
//         {
//             AccountId = asset1.AccountId,
//             CreatedAt = new DateTime(2022, 05, 01), 
//             Id = Guid.NewGuid(), 
//             Quantity = 50, 
//             Price = 190,
//             TypeId = (int)DealKind.Purchase, 
//             StockId = stock.Ticker,
//             CurrencyId = currency.Code,
//             UserId = user.Id
//         };
//         
//         var deal2 = new Deal
//         {
//             AccountId = asset2.AccountId,
//             CreatedAt = new DateTime(2022, 05, 02), 
//             Id = Guid.NewGuid(), 
//             Quantity = 11, 
//             Price = 230,
//             TypeId = (int)DealKind.Purchase, 
//             StockId = stock.Ticker,
//             CurrencyId = currency.Code,
//             UserId = user.Id
//         };
//         
//         var deal3 = new Deal
//         {
//             AccountId = asset1.AccountId,
//             CreatedAt = new DateTime(2022, 05, 03), 
//             Id = Guid.NewGuid(), 
//             Quantity = 5, 
//             Price = 210,
//             TypeId = (int)DealKind.Purchase, 
//             StockId = stock.Ticker,
//             CurrencyId = currency.Code,
//             UserId = user.Id
//         };
//         
//         var deal4 = new Deal
//         {
//             AccountId = asset1.AccountId,
//             CreatedAt = new DateTime(2022, 05, 04), 
//             Id = Guid.NewGuid(), 
//             Quantity = 3, 
//             Price = 100,
//             TypeId = (int)DealKind.Sale, 
//             StockId = stock.Ticker,
//             CurrencyId = currency.Code,
//             UserId = user.Id
//         };
//         
//         var deal5 = new Deal
//         {
//             AccountId = asset1.AccountId,
//             CreatedAt = new DateTime(2022, 05, 05), 
//             Id = Guid.NewGuid(), 
//             Quantity = 3, 
//             Price = 100,
//             TypeId = (int)DealKind.Sale, 
//             StockId = stock.Ticker,
//             CurrencyId = currency.Code,
//             UserId = user.Id
//         };
//         
//         var deal6 = new Deal
//         {
//             AccountId = asset2.AccountId,
//             CreatedAt = new DateTime(2022, 05, 06), 
//             Id = Guid.NewGuid(), 
//             Quantity = 1, 
//             Price = 55,
//             TypeId = (int)DealKind.Purchase, 
//             StockId = stock.Ticker,
//             CurrencyId = currency.Code,
//             UserId = user.Id
//         };
//         
//         var deal7 = new Deal
//         {
//             AccountId = asset3.AccountId,
//             CreatedAt = new DateTime(2022, 05, 03), 
//             Id = Guid.NewGuid(), 
//             Quantity = 5, 
//             Price = 210,
//             TypeId = (int)DealKind.Purchase, 
//             StockId = stock.Ticker,
//             CurrencyId = currency.Code,
//             UserId = test.Id
//         };
//         
//         var deal8 = new Deal
//         {
//             AccountId = asset3.AccountId,
//             CreatedAt = new DateTime(2022, 05, 04), 
//             Id = Guid.NewGuid(), 
//             Quantity = 3, 
//             Price = 100,
//             TypeId = (int)DealKind.Sale, 
//             StockId = stock.Ticker,
//             CurrencyId = currency.Code,
//             UserId = test.Id
//         };
//         
//         context.Assets.Add(asset1);
//         context.Assets.Add(asset2);
//         context.Assets.Add(asset3);
//         context.Deals.AddRange(new List<Deal>{deal1, deal2, deal3, deal4, deal5, deal6, deal7, deal8});
//         await context.SaveChangesAsync();
//         
//         mockUserAccessor.Setup(x => x.GetUsername()).Returns("admin");
//     
//         var service = new Services.PortfolioService(logger.Object, context, mockUserAccessor.Object, exchange.Object, mapper);
//     
//         var actual = await service.GetPortfolioAsync(Guid.Parse("f1fe6744-86a6-4293-b469-64404511840f"), CancellationToken.None);
//     
//         Assert.Single(actual.Assets);
//         Assert.Equal(192.04m, Math.Round(actual.Assets[0].AveragePrice, 2));
//         Assert.Equal(new decimal(stock.LastPrice), (decimal)actual.Assets[0].Stock.LastPrice);
//         Assert.Equal(stock.Type, actual.Assets[0].Stock.Type);
//         Assert.Equal(stock.SectorId, actual.Assets[0].Stock.Sector);
//         Assert.Equal(stock.Ticker, actual.Assets[0].Stock.Ticker);
//         Assert.Equal(stock.CompanyName, actual.Assets[0].Stock.CompanyName);
//         Assert.Equal(49, actual.Assets[0].Quantity);
//         mockUserAccessor.Verify(x => x.GetUsername());
//         mockUserAccessor.VerifyNoOtherCalls();
//         mockUserAccessor.VerifyAll();
//         logger.VerifyNoOtherCalls();
//         logger.VerifyAll();
//     }
//
//     [Theory]
//     [MemberData(nameof(Data))]
//     public async Task GetPortfolioAsync_Success(
//         string ticker, 
//         decimal quantity, 
//         decimal averagePrice, 
//         Tuple<int, decimal, DealKind, string, string?> data1, 
//         Tuple<int, decimal, DealKind, string, string?> data2, 
//         Tuple<int, decimal, DealKind, string, string?> data3, 
//         Tuple<int, decimal, DealKind, string, string?> data4)
//     {
//         var mockUserAccessor = new Mock<IUserAccessor>();
//         var logger = new Mock<ILogger<Services.PortfolioService>>();
//         var exchange = new Mock<IExchangeService>();
//         var context = SetupDbContext.Generate();
//         var mapper = new Mapper(_mapperConfiguration);
//
//         var stock = context.Stocks.First(x => x.Ticker == ticker);
//         var user = await context.Users.FirstAsync(x => x.UserName == "admin");
//         var asset1 = new Asset{AccountId = Guid.Parse("f1fe6744-86a6-4293-b469-64404511840f"), Id = Guid.NewGuid(), Quantity = quantity, StockId = ticker};
//         
//         var deal1 = new Deal
//         {
//             AccountId = asset1.AccountId,
//             CreatedAt = new DateTime(2022, 05, 01), 
//             Id = Guid.NewGuid(), 
//             Quantity = data1.Item1, 
//             Price = data1.Item2,
//             TypeId = (int)data1.Item3,
//             CurrencyId = data1.Item4,
//             StockId = data1.Item5,
//             UserId = user.Id
//         };
//         
//         var deal2 = new Deal
//         {
//             AccountId = asset1.AccountId,
//             CreatedAt = new DateTime(2022, 05, 02), 
//             Id = Guid.NewGuid(), 
//             Quantity = data2.Item1, 
//             Price = data2.Item2,
//             TypeId = (int)data2.Item3, 
//             CurrencyId = data2.Item4,
//             StockId = data2.Item5,
//             UserId = user.Id
//         };
//         
//         var deal3 = new Deal
//         {
//             AccountId = asset1.AccountId,
//             CreatedAt = new DateTime(2022, 05, 03), 
//             Id = Guid.NewGuid(), 
//             Quantity = data3.Item1, 
//             Price = data3.Item2,
//             TypeId = (int)data3.Item3, 
//             CurrencyId = data3.Item4,
//             StockId = data3.Item5,
//             UserId = user.Id
//         };
//         
//         var deal4 = new Deal
//         {
//             AccountId = asset1.AccountId,
//             CreatedAt = new DateTime(2022, 05, 04), 
//             Id = Guid.NewGuid(), 
//             Quantity = data4.Item1, 
//             Price = data4.Item2,
//             TypeId = (int)data4.Item3, 
//             CurrencyId = data4.Item4,
//             StockId = data4.Item5,
//             UserId = user.Id
//         };
//         
//         context.Assets.Add(asset1);
//         context.Deals.AddRange(new List<Deal>{deal1, deal2, deal3, deal4});
//         await context.SaveChangesAsync();
//         
//         mockUserAccessor.Setup(x => x.GetUsername()).Returns("admin");
//
//         var service = new Services.PortfolioService(logger.Object, context, mockUserAccessor.Object, exchange.Object, mapper);
//     
//         var actual = await service.GetPortfolioAsync(CancellationToken.None);
//
//         Assert.Single(actual.Assets);
//         Assert.Equal(averagePrice, Math.Round(actual.Assets[0].AveragePrice, 2));
//         Assert.Equal((decimal)stock.LastPrice, (decimal)actual.Assets[0].Stock.LastPrice);
//         Assert.Equal(stock.Type, actual.Assets[0].Stock.Type);
//         Assert.Equal(stock.SectorId, actual.Assets[0].Stock.Sector);
//         Assert.Equal(stock.Ticker, actual.Assets[0].Stock.Ticker);
//         Assert.Equal(stock.CompanyName, actual.Assets[0].Stock.CompanyName);
//         Assert.Equal(quantity, actual.Assets[0].Quantity);
//         mockUserAccessor.Verify(x => x.GetUsername());
//         mockUserAccessor.VerifyNoOtherCalls();
//         mockUserAccessor.VerifyAll();
//         logger.VerifyNoOtherCalls();
//         logger.VerifyAll();
//     }
//     
//     public static IEnumerable<object[]> Data(){
//         yield return new object[] { "USD", 556.32m, 75.00m, Tuple.Create<int, decimal, DealKind, string, string?>(1, 356.32m, DealKind.Add, "USD", null), Tuple.Create(445, 75.00m, DealKind.Purchase, "RUB", "USD"), Tuple.Create(345, 56.89m, DealKind.Sale, "RUB", "USD"), Tuple.Create<int, decimal, DealKind, string, string?>(1, 300.00m, DealKind.Withdrawal, "USD", null)};
//         yield return new object[] { "RUB", 16389.01m, 1.00m, Tuple.Create<int, decimal, DealKind, string, string?>(1, 6300.56m, DealKind.Add, "RUB", null), Tuple.Create<int, decimal, DealKind, string, string?>(1, 8579.06m, DealKind.Add, "RUB", null), Tuple.Create<int, decimal, DealKind, string, string?>(1, 7369.00m, DealKind.Add, "RUB", null), Tuple.Create<int, decimal, DealKind, string, string?>(1, 5859.61m, DealKind.Add, "RUB", null)};
//         yield return new object[] { "MTSS", 40m, 190.00m, Tuple.Create(3, 243.00m, DealKind.Purchase, "RUB", "MTSS"), Tuple.Create(3, 179.00m, DealKind.Sale, "RUB", "MTSS"), Tuple.Create(41, 190.00m, DealKind.Purchase, "RUB", "MTSS"), Tuple.Create(1, 163.00m, DealKind.Sale, "RUB", "MTSS")};
//         yield return new object[] { "MTSS", 8m, 177.62m, Tuple.Create(3, 250.00m, DealKind.Purchase, "RUB", "MTSS"), Tuple.Create(3, 210.00m, DealKind.Purchase, "RUB", "MTSS"), Tuple.Create(5, 220.00m, DealKind.Sale, "RUB", "MTSS"), Tuple.Create(7, 173.00m, DealKind.Purchase, "RUB", "MTSS")};
//         yield return new object[] { "MTSS", 12m, 196.67m, Tuple.Create(3, 250.00m, DealKind.Purchase, "RUB", "MTSS"), Tuple.Create(3, 210.00m, DealKind.Sale, "RUB", "MTSS"), Tuple.Create(5, 220.00m, DealKind.Purchase, "RUB", "MTSS"), Tuple.Create(7, 180.00m, DealKind.Purchase, "RUB", "MTSS")};
//     }
// }