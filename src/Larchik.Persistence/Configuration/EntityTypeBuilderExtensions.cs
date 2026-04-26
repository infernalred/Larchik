using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Larchik.Persistence.Configuration;

internal static class EntityTypeBuilderExtensions
{
    private const int MoneyPrecision = 18;
    private const int MoneyScale = 4;
    private const int QuantityPrecision = 18;
    private const int QuantityScale = 6;

    public static PropertyBuilder<DateTime> HasCreatedAt<TEntity>(
        this EntityTypeBuilder<TEntity> builder,
        Expression<Func<TEntity, DateTime>> propertyExpression,
        bool generatedOnAdd = false)
        where TEntity : class
    {
        var property = builder.Property(propertyExpression);
        return generatedOnAdd ? property.ValueGeneratedOnAdd() : property;
    }

    public static PropertyBuilder<DateTime> HasUpdatedAt<TEntity>(
        this EntityTypeBuilder<TEntity> builder,
        Expression<Func<TEntity, DateTime>> propertyExpression)
        where TEntity : class =>
        builder.Property(propertyExpression);

    public static PropertyBuilder<decimal> HasMoneyPrecision<TEntity>(
        this EntityTypeBuilder<TEntity> builder,
        Expression<Func<TEntity, decimal>> propertyExpression,
        int precision = MoneyPrecision,
        int scale = MoneyScale)
        where TEntity : class =>
        builder.Property(propertyExpression).HasPrecision(precision, scale);

    public static PropertyBuilder<decimal> HasQuantityPrecision<TEntity>(
        this EntityTypeBuilder<TEntity> builder,
        Expression<Func<TEntity, decimal>> propertyExpression,
        int precision = QuantityPrecision,
        int scale = QuantityScale)
        where TEntity : class =>
        builder.Property(propertyExpression).HasPrecision(precision, scale);
}
