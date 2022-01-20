using System.Reflection;
using Larchik.Domain;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Larchik.Persistence.Context;

public abstract class DataContextBase : IdentityDbContext<AppUser>
{
    protected DataContextBase(DbContextOptions options) : base(options) { }
    
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
            
        var applyGenericMethod = typeof(ModelBuilder).GetMethods(BindingFlags.Instance | BindingFlags.Public).First(x => x.Name == "ApplyConfiguration");
        foreach (var type in Assembly.GetExecutingAssembly().GetTypes().Where(c => c.IsClass && !c.IsAbstract && !c.ContainsGenericParameters))
        {
            foreach (var item in type.GetInterfaces())
            {
                if (!item.IsConstructedGenericType || item.GetGenericTypeDefinition() != typeof(IEntityTypeConfiguration<>))
                {
                    continue;
                }
            
                var applyConcreteMethod = applyGenericMethod.MakeGenericMethod(item.GenericTypeArguments[0]);
                applyConcreteMethod.Invoke(builder, new[] { Activator.CreateInstance(type) });
                break;
            }
        }
    }
}