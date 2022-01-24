using Larchik.Domain;
using Microsoft.EntityFrameworkCore;

namespace Larchik.Persistence.Context;

public interface IDataContext
{
    DbSet<AppUser> Users { get; set; }
    DbSet<Broker> Brokers { get; set; }
    DbSet<Currency> Currencies { get; set; }
    DbSet<Account> Accounts { get; set; }
    DbSet<Asset> Assets { get; set; }
    DbSet<Money> Monies { get; set; }
    DbSet<StockType> StockTypes { get; set; }
    DbSet<Sector> Sectors { get; set; }
}