using Larchik.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Larchik.Persistence.Configuration;

public class CategoryModelConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.Property(x => x.Name).HasMaxLength(100);

        var category1 = new Category
        {
            Id = 1,
            Name = "Валюта"
        };
        var category2 = new Category
        {
            Id = 2,
            Name = "Финансы и банки"
        };
        var category3 = new Category
        {
            Id = 3,
            Name = "Телекоммуникации"
        };
        var category4 = new Category
        {
            Id = 4,
            Name = "Информационные технологии"
        };
        var category5 = new Category
        {
            Id = 5,
            Name = "Энергетика"
        };
        var category6 = new Category
        {
            Id = 6,
            Name = "Потребительские товары"
        };
        var category7 = new Category
        {
            Id = 7,
            Name = "Недвижимость"
        };
        var category8 = new Category
        {
            Id = 8,
            Name = "Валюта"
        };
        var category9 = new Category
        {
            Id = 9,
            Name = "Электроэнергетика"
        };
        var category10 = new Category
        {
            Id = 10,
            Name = "Сырьевая промышленность"
        };

        builder.HasData(category1, category2, category3, category4, category5, category6, category7,
            category8, category9, category10);
    }
}