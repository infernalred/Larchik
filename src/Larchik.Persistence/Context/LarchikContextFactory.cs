using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Larchik.Persistence.Context;

public class LarchikContextFactory : IDesignTimeDbContextFactory<LarchikContext>
{
    public LarchikContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
            ?? Environment.GetEnvironmentVariable("DATABASE_URL")
            ?? "Host=localhost;Port=5432;Database=larchik;Username=postgres;Password=postgres";

        var optionsBuilder = new DbContextOptionsBuilder<LarchikContext>();
        optionsBuilder.UseNpgsql(connectionString);
        optionsBuilder.UseSnakeCaseNamingConvention();

        return new LarchikContext(optionsBuilder.Options);
    }
}
