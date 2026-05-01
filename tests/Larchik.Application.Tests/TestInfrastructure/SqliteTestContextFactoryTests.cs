using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Larchik.Application.Tests.TestInfrastructure;

public sealed class SqliteTestContextFactoryTests
{
    [Fact]
    public void Create_ReturnsUsableContext_WithOpenConnection()
    {
        using var database = SqliteTestContextFactory.Create();

        Assert.Equal(QueryTrackingBehavior.NoTracking, database.Context.ChangeTracker.QueryTrackingBehavior);
        Assert.Equal("Open", database.Connection.State.ToString());
        Assert.True(database.Context.Database.CanConnect());
        Assert.NotNull(database.Context.Model.FindEntityType(typeof(Larchik.Persistence.Entities.Instrument)));
    }
}
