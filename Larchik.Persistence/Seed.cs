using Larchik.Persistence.Context;
using Larchik.Persistence.Entity;
using Larchik.Persistence.Enum;
using Microsoft.AspNetCore.Identity;

namespace Larchik.Persistence;

public static class Seed
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
            var rub = new Currency { Id = "RUB" };
            var usd = new Currency { Id = "USD" };
            var eur = new Currency { Id = "EUR" };

            var currencies = new List<Currency>
            {
                rub, usd, eur
            };
            
            await context.AddRangeAsync(currencies);
            await context.SaveChangesAsync();
        }
        
        if (!context.Sectors.Any())
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
            
            await context.AddRangeAsync(sectors);
            await context.SaveChangesAsync();
        }

        if (!context.Stocks.Any())
        {
            var money1 = new Stock { Ticker = "RUB", Name = "Рубль", CurrencyId = "RUB", SectorId = "Валюта", Isin = "RUB", LastPrice = 1, Kind = StockKind.Money};
            var money2 = new Stock { Ticker = "USD", Name = "Доллар США", CurrencyId = "USD", SectorId = "Валюта", Isin = "USD", LastPrice = 121, Kind = StockKind.Money };
            var money3 = new Stock { Ticker = "EUR", Name = "Евро", CurrencyId = "EUR", SectorId = "Валюта", Isin = "EUR", LastPrice = 134, Kind = StockKind.Money };

            var stocks = new List<Stock>
            {
                money1, money2, money3
            };
            
            await context.AddRangeAsync(stocks);
            await context.SaveChangesAsync();
        }

        // if (!context.DealTypes.Any())
        // {
        //     var dealType1 = new DealType { Id = 1, Code = "Пополнение" };
        //     var dealType2 = new DealType { Id = 2, Code = "Вывод" };
        //     var dealType3 = new DealType { Id = 3, Code = "Покупка" };
        //     var dealType4 = new DealType { Id = 4, Code = "Продажа" };
        //     var dealType5 = new DealType { Id = 5, Code = "Комиссия" };
        //     var dealType6 = new DealType { Id = 6, Code = "Налог" };
        //     var dealType7 = new DealType { Id = 7, Code = "Дивиденды" };
        //
        //     var dealTypes = new List<DealType>
        //     {
        //         dealType1, dealType2, dealType3, dealType4, dealType5, dealType6, dealType7
        //     };
        //
        //     await context.AddRangeAsync(dealTypes);
        //     await context.SaveChangesAsync();
        // }

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