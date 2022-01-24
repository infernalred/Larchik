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
            var user = new AppUser {DisplayName = "Admin", UserName = "admin", Email = "admin@admin.com"};

            await userManager.CreateAsync(user, "Pa$$w0rd");
        }

        if (!context.Brokers.Any())
        {
            var broker1 = new Broker { Id = Guid.Parse("de4ecef8-14d9-4a32-8dfd-0ea9e5777269"), Name = "Акционерное общество \"Тинькофф Банк\"", Inn = "7710140679" };
            var broker2 = new Broker { Id = Guid.Parse("dd357ed2-cadf-43ff-8d01-ed25a97b5026"), Name = "Акционерное общество ВТБ Капитал", Inn = "7703585780" };
            var broker3 = new Broker { Id = Guid.Parse("99c2a5c6-e038-46c5-96fd-1b156d316d70"), Name = "Публичное акционерное общество Банк \"Финансовая Корпорация Открытие\"", Inn = "7706092528" };
            
            var brokers = new List<Broker>
            {
                broker1, broker2, broker3
            };

            await context.AddRangeAsync(brokers);
            await context.SaveChangesAsync();
        }
    }
}