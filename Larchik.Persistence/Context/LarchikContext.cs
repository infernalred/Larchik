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
    public DbSet<Stock> Stocks { get; set; } = null!;
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
    }
}