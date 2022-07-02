using Larchik.Domain;
using Microsoft.EntityFrameworkCore;

namespace Larchik.Persistence.Context;

public class DataContext : DataContextBase, IDataContext
{
    public DataContext(DbContextOptions<DataContext> options) : base(options) { }
    public DbSet<Currency> Currencies { get; set; } = null!;
    public DbSet<Account> Accounts { get; set; } = null!;
    public DbSet<Asset> Assets { get; set; } = null!;
    public DbSet<Stock> Stocks { get; set; } = null!;
    public DbSet<StockType> StockTypes { get; set; } = null!;
    public DbSet<Sector> Sectors { get; set; } = null!;
    public DbSet<Operation> Operations { get; set; } = null!;
    public DbSet<Deal> Deals { get; set; } = null!;
    public DbSet<Exchange> Exchanges { get; set; }
}