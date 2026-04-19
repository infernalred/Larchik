using Larchik.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Larchik.Persistence.Configuration;

public class CategoryModelConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.Property(x => x.Name).HasMaxLength(100);

        builder.HasData(
            new Category { Id = 1, Name = "Валюта" },
            new Category { Id = 2, Name = "Финансы и банки" },
            new Category { Id = 3, Name = "Телекоммуникации" },
            new Category { Id = 4, Name = "Информационные технологии" },
            new Category { Id = 5, Name = "Энергетика" },
            new Category { Id = 6, Name = "Потребительские товары" },
            new Category { Id = 7, Name = "Недвижимость" },
            new Category { Id = 8, Name = "Валюта" },
            new Category { Id = 9, Name = "Электроэнергетика" },
            new Category { Id = 10, Name = "Сырьевая промышленность" });
    }
}
