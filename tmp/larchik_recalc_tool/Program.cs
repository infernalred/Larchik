using Larchik.Infrastructure.Recalculation;
using Larchik.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

if (args.Length == 0 || !Guid.TryParse(args[0], out var portfolioId))
{
    Console.Error.WriteLine("Usage: larchik_recalc_tool <portfolioId>");
    return 1;
}

const string connectionString = "Server=localhost;Port=5432;User Id=postgres;Password=Qwerty1;Database=larchik";

var options = new DbContextOptionsBuilder<LarchikContext>()
    .UseNpgsql(connectionString, o => o.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery))
    .UseSnakeCaseNamingConvention()
    .Options;

await using var context = new LarchikContext(options);
var fromDate = await context.Operations
    .AsNoTracking()
    .Where(x => x.PortfolioId == portfolioId)
    .MinAsync(x => (DateTime?)x.TradeDate);

if (fromDate is null)
{
    Console.Error.WriteLine($"No operations found for portfolio {portfolioId}");
    return 2;
}

using var loggerFactory = LoggerFactory.Create(builder => builder.AddSimpleConsole());
var logger = loggerFactory.CreateLogger<PortfolioRecalcService>();
var recalc = new PortfolioRecalcService(context, logger);

Console.WriteLine($"Rebuilding snapshots for {portfolioId} from {fromDate:yyyy-MM-dd}...");
await recalc.ScheduleRebuild(portfolioId, fromDate.Value, CancellationToken.None);
Console.WriteLine("Done.");
return 0;
