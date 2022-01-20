using Microsoft.EntityFrameworkCore;

namespace Larchik.Persistence.Context;

public class DataContext : DataContextBase, IDataContext
{
    public DataContext(DbContextOptions<DataContext> options) : base(options) { }
}