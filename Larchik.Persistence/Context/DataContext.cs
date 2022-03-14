using Larchik.Domain;
using Microsoft.EntityFrameworkCore;

namespace Larchik.Persistence.Context;

public class DataContext : DataContextBase, IDataContext
{
    public DataContext(DbContextOptions<DataContext> options) : base(options) { }
    public DbSet<Broker> Brokers { get; set; }
    public DbSet<Currency> Currencies { get; set; }
    public DbSet<Account> Accounts { get; set; }
    public DbSet<Asset> Assets { get; set; }
    public DbSet<Stock> Stocks { get; set; }
    public DbSet<StockType> StockTypes { get; set; }
    public DbSet<Sector> Sectors { get; set; }
    public DbSet<Operation> Operations { get; set; }
    public DbSet<Deal> Deals { get; set; }
}