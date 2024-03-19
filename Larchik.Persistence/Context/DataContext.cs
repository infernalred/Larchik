using System.Reflection;
using Larchik.Persistence.Entity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Larchik.Persistence.Context;

public class DataContext(DbContextOptions<DataContext> options)
    : IdentityDbContext<IdentityUser>(options)
{
    // public DbSet<Currency> Currencies { get; set; } = null!;
    // public DbSet<Account> Accounts { get; set; } = null!;
    // public DbSet<Asset> Assets { get; set; } = null!;
    // public DbSet<Stock> Stocks { get; set; } = null!;
    // public DbSet<Sector> Sectors { get; set; } = null!;
    // public DbSet<Operation> Operations { get; set; } = null!;
    // public DbSet<Exchange> Exchanges { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
    }
}