using System.Reflection;
using Larchik.Persistence.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Larchik.Persistence.Context;

public class LarchikContext(DbContextOptions<LarchikContext> options)
    : IdentityDbContext<AppUser, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<Currency> Currencies { get; set; } = null!;
    public DbSet<Category> Categories { get; set; } = null!;
    public DbSet<Instrument> Instruments { get; set; } = null!;
    public DbSet<Broker> Brokers { get; set; } = null!;
    public DbSet<Portfolio> Portfolios { get; set; } = null!;
    public DbSet<CashBalance> CashBalances { get; set; } = null!;
    public DbSet<Operation> Operations { get; set; } = null!;
    public DbSet<Price> Prices { get; set; } = null!;
    public DbSet<FxRate> FxRates { get; set; } = null!;
    public DbSet<Lot> Lots { get; set; } = null!;
    public DbSet<JobDefinition> JobDefinitions { get; set; } = null!;
    public DbSet<JobRun> JobRuns { get; set; } = null!;
    public DbSet<PositionSnapshot> PositionSnapshots { get; set; } = null!;
    public DbSet<PortfolioSnapshot> PortfolioSnapshots { get; set; } = null!;
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
    }
}
