using Larchik.Persistence.Context;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Larchik.Application.Tests.TestInfrastructure;

internal static class SqliteTestContextFactory
{
    public static SqliteTestDatabase Create(bool ensureCreated = true)
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<LarchikContext>()
            .UseSqlite(connection)
            .Options;

        var context = new LarchikContext(options);
        if (ensureCreated)
        {
            context.Database.EnsureCreated();
        }

        return new SqliteTestDatabase(connection, context);
    }
}

internal sealed class SqliteTestDatabase(SqliteConnection connection, LarchikContext context) : IDisposable, IAsyncDisposable
{
    public SqliteConnection Connection { get; } = connection;
    public LarchikContext Context { get; } = context;

    public void Dispose()
    {
        Context.Dispose();
        Connection.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        await Context.DisposeAsync();
        await Connection.DisposeAsync();
    }
}
