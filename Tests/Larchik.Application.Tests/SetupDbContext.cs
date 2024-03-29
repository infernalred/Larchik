﻿using System;
using System.Collections.Generic;
using Larchik.Domain;
using Larchik.Domain.Enum;
using Larchik.Persistence.Context;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Larchik.Application.Tests;

public static class SetupDbContext
{
    public static DataContext Generate()
    {
        var options = CreateNewContextOptions();
        var context = new DataContext(options);
        context.Database.EnsureCreated();
        CreateUserAndAccount(context);
        CreateCurrency(context);
        CreateStockTypes(context);
        CreateSectors(context);
        CreateDealTypes(context);
        CreateStocks(context);
        CreateExchanges(context);

        context.SaveChanges();

        return context;
    }
    
    private static void CreateUserAndAccount(DataContext context)
    {
        var user = new AppUser {DisplayName = "Admin", UserName = "admin", Email = "admin@admin.com"};
        var test1 = new AppUser {DisplayName = "Test", UserName = "test", Email = "test@test.com"};
        context.Users.Add(user);
        context.Users.Add(test1);
        
        var account1 = new Account{Id = Guid.Parse("f1fe6744-86a6-4293-b469-64404511840f"), User = user, Name = "Счет1"};
        var account2 = new Account{Id = Guid.Parse("a4c15931-70ca-4a55-9fac-0715c4a56264"), User = test1, Name = "Счет1"};
        var account3 = new Account{Id = Guid.Parse("860C80E3-AE30-4179-8E0E-C2C0DBA68268"), User = user, Name = "Счет2"};

        var accounts = new List<Account>
        {
            account1, account2, account3
        };
            
        context.Accounts.AddRange(accounts);
    }

    private static void CreateCurrency(IDataContext context)
    {
        var rub = new Currency { Code = "RUB" };
        var usd = new Currency { Code = "USD" };
        var eur = new Currency { Code = "EUR" };

        var currencies = new List<Currency>
        {
            rub, usd, eur
        };
            
        context.Currencies.AddRange(currencies);
    }
    
    private static void CreateStockTypes(IDataContext context)
    {
        var type1 = new StockType { Id = 1, Code = "SHARE" };
        var type2 = new StockType { Id = 2, Code = "BOND" };
        var type3 = new StockType { Id = 3, Code = "ETF" };
        var type4 = new StockType { Id = 4, Code = "MONEY" };

        var stockTypes = new List<StockType>
        {
            type1, type2, type3, type4
        };
            
        context.StockTypes.AddRange(stockTypes);
    }
    
    private static void CreateSectors(IDataContext context)
    {
        var sector1 = new Sector { Code = "Energy" };
        var sector2 = new Sector { Code = "Materials" };
        var sector3 = new Sector { Code = "Industrials" };
        var sector4 = new Sector { Code = "Utilities" };
        var sector5 = new Sector { Code = "Healthcare" };
        var sector6 = new Sector { Code = "Financial" };
        var sector7 = new Sector { Code = "Consumer" };
        var sector8 = new Sector { Code = "Other" };
        var sector9 = new Sector { Code = "Information Technology" };
        var sector10 = new Sector { Code = "Communication Services" };
        var sector11 = new Sector { Code = "Real Estate" };
        var sector12 = new Sector { Code = "IT" };
        var sector13 = new Sector { Code = "Валюта" };
        var sector14 = new Sector { Code = "Telecom" };
        var sector15 = new Sector { Code = "Ecomaterials" };
        var sector16 = new Sector { Code = "Green Buildings" };
        var sector17 = new Sector { Code = "Green Energy" };
        var sector18 = new Sector { Code = "Electrocars" };

        var sectors = new List<Sector>
        {
            sector1, sector2, sector3, sector4, sector5, sector6, sector7, sector8, sector9, sector10, sector11, sector12, sector13, sector14, sector15, sector16, sector17, sector18
        };
            
        context.Sectors.AddRange(sectors);
    }
    
    private static void CreateDealTypes(IDataContext context)
    {
        var dealType1 = new DealType { Id = 1, Code = "Пополнение" };
        var dealType2 = new DealType { Id = 2, Code = "Вывод" };
        var dealType3 = new DealType { Id = 3, Code = "Покупка" };
        var dealType4 = new DealType { Id = 4, Code = "Продажа" };
        var dealType5 = new DealType { Id = 5, Code = "Комиссия" };
        var dealType6 = new DealType { Id = 6, Code = "Налог" };
        var dealType7 = new DealType { Id = 7, Code = "Дивиденды" };

        var dealTypes = new List<DealType>
        {
            dealType1, dealType2, dealType3, dealType4, dealType5, dealType6, dealType7
        };

        context.DealTypes.AddRange(dealTypes);
    }
    
    private static void CreateStocks(IDataContext context)
    {
        var money1 = new Stock { Ticker = "RUB", CompanyName = "", CurrencyId = "RUB", TypeId = "MONEY", SectorId = "Валюта", Figi = "RUB", LastPrice = 1, Type = StockKind.Money };
        var money2 = new Stock { Ticker = "USD", CompanyName = "", CurrencyId = "USD", TypeId = "MONEY", SectorId = "Валюта", Figi = "USD", LastPrice = 121, Type = StockKind.Money };
        var money3 = new Stock { Ticker = "EUR", CompanyName = "", CurrencyId = "EUR", TypeId = "MONEY", SectorId = "Валюта", Figi = "EUR", LastPrice = 134, Type = StockKind.Money };
        var stock1 = new Stock { Ticker = "MTSS", CompanyName = "МТС", CurrencyId = "RUB", TypeId = "SHARE", SectorId = "Telecom", Figi = "MTSS", LastPrice = 249.5, Type = StockKind.Share };
        var stock2 = new Stock { Ticker = "SBER", CompanyName = "СБЕРБАНК", CurrencyId = "RUB", TypeId = "SHARE", SectorId = "Financial", Figi = "SBER", LastPrice = 157, Type = StockKind.Share };

        var stocks = new List<Stock>
        {
            money1, money2, money3, stock1, stock2
        };
            
        context.Stocks.AddRange(stocks);
    }

    private static void CreateExchanges(IDataContext context)
    {
        var exchanges = new List<Exchange>
        {
            new() {Code = "USD_RUB", Nominal = 1, Date = new DateOnly(2022, 05, 01), Rate = 70.31},
        };
        
        context.Exchanges.AddRange(exchanges);
    }
    
    private static DbContextOptions<DataContext> CreateNewContextOptions()
    {
        var connection = new SqliteConnection("Filename=:memory:");
        connection.Open();

        var builder = new DbContextOptionsBuilder<DataContext>()
            .UseSqlite(connection);

        return builder.Options;
    }
}