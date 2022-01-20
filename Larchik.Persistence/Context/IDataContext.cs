using Larchik.Domain;
using Microsoft.EntityFrameworkCore;

namespace Larchik.Persistence.Context;

public interface IDataContext
{
    DbSet<AppUser> Users { get; set; }
}