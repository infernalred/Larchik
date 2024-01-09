using System.Reflection;
using Larchik.Domain;
using Larchik.Persistence.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Larchik.Persistence.Context;

public class DataContext(DbContextOptions options) : IdentityDbContext<AppUser, AppRole, Guid>(options)
{
    public DbSet<Currency> Currencies { get; set; } = null!;
    public DbSet<Account> Accounts { get; set; } = null!;
    public DbSet<Asset> Assets { get; set; } = null!;
    public DbSet<Stock> Stock { get; set; } = null!;
    public DbSet<StockType> StockTypes { get; set; } = null!;
    public DbSet<Sector> Sectors { get; set; } = null!;
    public DbSet<Operation> Operations { get; set; } = null!;
    public DbSet<DealType> DealTypes { get; set; } = null!;
    public DbSet<Deal> Deals { get; set; } = null!;
    public DbSet<Exchange> Exchanges { get; set; } = null!;
    public DbSet<Log> Logs { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
    }
}