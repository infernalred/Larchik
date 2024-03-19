// using System;
// using System.Collections.Generic;
// using System.Threading.Tasks;
// using Larchik.Persistence.Entity;
// using Microsoft.Extensions.Caching.Memory;
// using Moq;
// using Xunit;
//
// namespace Larchik.Application.Tests.ExchangeService;
//
// public class GetAmountTests
// {
//     private delegate void OutDelegate<in TIn, TOut>(TIn input, out TOut output);
//
//     [Fact]
//     public async Task GetAmount_FromContext_Variant1()
//     {
//         var context = SetupDbContext.Generate();
//         var cache = new Mock<IMemoryCache>();
//         var cacheEntry = new Mock<ICacheEntry>();
//
//         var deal = new Operation
//         {
//             CreatedAt = new DateTime(2022, 05, 01),
//             Amount = 65.78m
//         };
//
//         var exchanges = new List<Exchange>
//         {
//             new() {Code = "USD_RUB", Nominal = 1, Date = new DateOnly(2022, 03, 30), Rate = 65.00},
//             new() {Code = "USD_RUB", Nominal = 1, Date = new DateOnly(2022, 05, 02), Rate = 73.00}
//         };
//         context.Exchanges.AddRange(exchanges);
//         await context.SaveChangesAsync();
//
//         object exchange;
//         cache.Setup(x => x.TryGetValue(It.IsAny<object>(), out exchange)).Returns(false);
//         cache.Setup(x => x.CreateEntry(It.IsAny<object>())).Returns(cacheEntry.Object);
//
//         var exchangeService = new Services.ExchangeService(context, cache.Object);
//
//         var result = await exchangeService.GetAmountAsync(deal, "USD_RUB");
//
//         Assert.Equal(4624.9918m, result);
//     }
//
//     [Fact]
//     public async Task GetAmount_FromCache_Variant1()
//     {
//         var context = SetupDbContext.Generate();
//         var cache = new Mock<IMemoryCache>();
//         var cacheEntry = new Mock<ICacheEntry>();
//
//         var deal = new Operation
//         {
//             CreatedAt = new DateTime(2022, 05, 01),
//             Amount = 65.78m
//         };
//
//         var exchanges = new List<Exchange>
//         {
//             new() {Code = "USD_RUB", Nominal = 1, Date = new DateOnly(2022, 04, 30), Rate = 65.00},
//             new() {Code = "USD_RUB", Nominal = 1, Date = new DateOnly(2022, 05, 02), Rate = 73.00}
//         };
//         context.Exchanges.AddRange(exchanges);
//         await context.SaveChangesAsync();
//
//         object exchange;
//
//         cache.Setup(x => x.TryGetValue(It.IsAny<object>(), out exchange))
//             .Callback(new OutDelegate<object, object>((object k, out object v) =>
//                 v = new Exchange {Code = "USD_RUB", Nominal = 1, Date = new DateOnly(2022, 05, 01), Rate = 70.31}))
//             .Returns(true);
//         cache.Setup(x => x.CreateEntry(It.IsAny<object>())).Returns(cacheEntry.Object);
//
//         var exchangeService = new Services.ExchangeService(context, cache.Object);
//
//         var result = await exchangeService.GetAmountAsync(deal, "USD_RUB");
//
//         Assert.Equal(4624.9918m, result);
//     }
//
//     [Fact]
//     public async Task GetAmount_FromContext_Variant2()
//     {
//         var context = SetupDbContext.Generate();
//         var cache = new Mock<IMemoryCache>();
//         var cacheEntry = new Mock<ICacheEntry>();
//
//         var deal = new Operation
//         {
//             CreatedAt = new DateTime(2022, 05, 04),
//             Amount = 65.78m
//         };
//
//         var exchanges = new List<Exchange>
//         {
//             new() {Code = "USD_RUB", Nominal = 1, Date = new DateOnly(2022, 04, 30), Rate = 65.00},
//             new() {Code = "USD_RUB", Nominal = 1, Date = new DateOnly(2022, 05, 02), Rate = 73.21},
//             new() {Code = "USD_RUB", Nominal = 1, Date = new DateOnly(2022, 05, 05), Rate = 69.44}
//         };
//         context.Exchanges.AddRange(exchanges);
//         await context.SaveChangesAsync();
//
//         object exchange;
//         cache.Setup(x => x.TryGetValue(It.IsAny<object>(), out exchange)).Returns(false);
//         cache.Setup(x => x.CreateEntry(It.IsAny<object>())).Returns(cacheEntry.Object);
//
//         var exchangeService = new Services.ExchangeService(context, cache.Object);
//
//         var result = await exchangeService.GetAmountAsync(deal, "USD_RUB");
//
//         Assert.Equal(4815.7538m, result);
//     }
//
//     [Fact]
//     public async Task GetAmount_FromCache_Variant2()
//     {
//         var context = SetupDbContext.Generate();
//         var cache = new Mock<IMemoryCache>();
//         var cacheEntry = new Mock<ICacheEntry>();
//
//         var deal = new Operation
//         {
//             CreatedAt = new DateTime(2022, 05, 01),
//             Amount = 65.78m
//         };
//
//         var exchanges = new List<Exchange>
//         {
//             new() {Code = "USD_RUB", Nominal = 1, Date = new DateOnly(2022, 04, 30), Rate = 65.00},
//             new() {Code = "USD_RUB", Nominal = 1, Date = new DateOnly(2022, 05, 02), Rate = 73.21},
//             new() {Code = "USD_RUB", Nominal = 1, Date = new DateOnly(2022, 05, 05), Rate = 69.44}
//         };
//         context.Exchanges.AddRange(exchanges);
//         await context.SaveChangesAsync();
//
//         object exchange;
//
//         cache.Setup(x => x.TryGetValue(It.IsAny<object>(), out exchange))
//             .Callback(new OutDelegate<object, object>((object k, out object v) =>
//                 v = new Exchange {Code = "USD_RUB", Nominal = 1, Date = new DateOnly(2022, 05, 02), Rate = 73.21}))
//             .Returns(true);
//         cache.Setup(x => x.CreateEntry(It.IsAny<object>())).Returns(cacheEntry.Object);
//
//         var exchangeService = new Services.ExchangeService(context, cache.Object);
//
//         var result = await exchangeService.GetAmountAsync(deal, "USD_RUB");
//
//         Assert.Equal(4815.7538m, result);
//     }
// }