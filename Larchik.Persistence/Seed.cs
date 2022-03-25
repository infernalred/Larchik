using Larchik.Domain;
using Larchik.Persistence.Context;
using Microsoft.AspNetCore.Identity;

namespace Larchik.Persistence;

public class Seed
{
    public static async Task SeedData(DataContext context, UserManager<AppUser> userManager)
    {
        if (!userManager.Users.Any())
        {
            var admin = new AppUser {DisplayName = "Admin", UserName = "admin", Email = "admin@admin.com"};
            var user1 = new AppUser {DisplayName = "User1", UserName = "user1", Email = "user1@user.com"};

            await userManager.CreateAsync(admin, "Pa$$w0rd");
            await userManager.CreateAsync(user1, "Pa$$w0rd");
        }

        if (!context.Currencies.Any())
        {
            var rub = new Currency { Code = "RUB" };
            var usd = new Currency { Code = "USD" };
            var eur = new Currency { Code = "EUR" };

            var currencies = new List<Currency>
            {
                rub, usd, eur
            };
            
            await context.AddRangeAsync(currencies);
            await context.SaveChangesAsync();
        }
        
        if (!context.StockTypes.Any())
        {
            var type1 = new StockType { Code = "SHARE" };
            var type2 = new StockType { Code = "BOND" };
            var type3 = new StockType { Code = "ETF" };
            var type4 = new StockType { Code = "MONEY" };

            var stockTypes = new List<StockType>
            {
                type1, type2, type3, type4
            };
            
            await context.AddRangeAsync(stockTypes);
            await context.SaveChangesAsync();
        }
        
        if (!context.Sectors.Any())
        {
            var sector1 = new Sector { Code = "Energy" };
            var sector2 = new Sector { Code = "Materials" };
            var sector3 = new Sector { Code = "Industrials" };
            var sector4 = new Sector { Code = "Utilities" };
            var sector5 = new Sector { Code = "Healthcare" };
            var sector6 = new Sector { Code = "Financials" };
            var sector7 = new Sector { Code = "Consumer Discretionary" };
            var sector8 = new Sector { Code = "Consumer Staples" };
            var sector9 = new Sector { Code = "Information Technology" };
            var sector10 = new Sector { Code = "Communication Services" };
            var sector11 = new Sector { Code = "Real Estate" };
            var sector12 = new Sector { Code = "Валюта" };

            var sectors = new List<Sector>
            {
                sector1, sector2, sector3, sector4, sector5, sector6, sector7, sector8, sector9, sector10, sector11, sector12
            };
            
            await context.AddRangeAsync(sectors);
            await context.SaveChangesAsync();
        }

        if (!context.Stocks.Any())
        {
            var stock1 = new Stock { Ticker = "AAPL", CompanyName = "Apple", CurrencyId = "USD", TypeId = "SHARE", SectorId = "Information Technology"};
            var stock2 = new Stock { Ticker = "MSFT", CompanyName = "Microsoft", CurrencyId = "USD", TypeId = "SHARE", SectorId = "Information Technology"};
            var stock3 = new Stock { Ticker = "SBER", CompanyName = "Sberbank", CurrencyId = "RUB", TypeId = "SHARE", SectorId = "Financials"};
            var stock4 = new Stock { Ticker = "MTSS", CompanyName = "MTS", CurrencyId = "RUB", TypeId = "SHARE", SectorId = "Communication Services"};

            var money1 = new Stock { Ticker = "RUB", CompanyName = "", CurrencyId = "RUB", TypeId = "MONEY", SectorId = "Валюта" };
            var money2 = new Stock { Ticker = "USD", CompanyName = "", CurrencyId = "USD", TypeId = "MONEY", SectorId = "Валюта" };
            var money3 = new Stock { Ticker = "EUR", CompanyName = "", CurrencyId = "EUR", TypeId = "MONEY", SectorId = "Валюта" };
            
            var stocks = new List<Stock>
            {
                stock1, stock2, stock3, stock4, money1, money2, money3
            };
            
            await context.AddRangeAsync(stocks);
            await context.SaveChangesAsync();
        }

        if (!context.Operations.Any())
        {
            var operation1 = new Operation { Code = "Пополнение" };
            var operation2 = new Operation { Code = "Вывод" };
            var operation3 = new Operation { Code = "Покупка" };
            var operation4 = new Operation { Code = "Продажа" };
            var operation5 = new Operation { Code = "Комиссия" };
            var operation6 = new Operation { Code = "Налог" };
            var operation7 = new Operation { Code = "Дивиденды" };
            
            var operations = new List<Operation>
            {
                operation1, operation2, operation3, operation4, operation5, operation6, operation7
            };
            
            await context.AddRangeAsync(operations);
            await context.SaveChangesAsync();
        }

        if (!context.Accounts.Any())
        {
            var admin = context.Users.First(x => x.UserName == "admin");
            var user1 = context.Users.First(x => x.UserName == "user1");
            
            var account1 = new Account{Id = Guid.Parse("f1fe6744-86a6-4293-b469-64404511840f"), User = admin, Name = "Счет1"};
            var account2 = new Account{Id = Guid.Parse("a4c15931-70ca-4a55-9fac-0715c4a56264"), User = user1, Name = "Счет1"};

            var accounts = new List<Account>
            {
                account1, account2
            };
            
            await context.AddRangeAsync(accounts);
            await context.SaveChangesAsync();
        }
    }
}