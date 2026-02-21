using Larchik.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Larchik.Persistence.Configuration;

public class BrokerModelConfiguration : IEntityTypeConfiguration<Broker>
{
    public void Configure(EntityTypeBuilder<Broker> builder)
    {
        builder.Property(x => x.Name).IsRequired().HasMaxLength(120);
        builder.Property(x => x.Country).HasMaxLength(100);

        builder.HasIndex(x => x.Name).IsUnique();

        builder.HasData(
            new Broker { Id = Guid.Parse("0d30ef6c-f09f-44da-8bf0-a20001c4c001"), Name = "СберБанк", Country = "Россия" },
            new Broker { Id = Guid.Parse("4ee304a8-6f0a-490f-bfa5-58f6f958b002"), Name = "ВТБ", Country = "Россия" },
            new Broker { Id = Guid.Parse("f6f784ea-b520-4bc5-8a32-9a17f1637003"), Name = "Т-Банк", Country = "Россия" },
            new Broker { Id = Guid.Parse("f444bdaf-bcb7-41fa-b22d-5a2fd7d5e004"), Name = "Альфа-Банк", Country = "Россия" },
            new Broker { Id = Guid.Parse("47096031-0778-4a7e-8552-57a7f6b4d005"), Name = "Газпромбанк", Country = "Россия" },
            new Broker { Id = Guid.Parse("4f3178f2-218d-4802-8e38-68af3a972006"), Name = "Россельхозбанк", Country = "Россия" },
            new Broker { Id = Guid.Parse("802c7a07-532b-4c22-a8ac-05f984052007"), Name = "Промсвязьбанк", Country = "Россия" },
            new Broker { Id = Guid.Parse("65db9cc2-5f8f-4a53-a37a-6abcf217d008"), Name = "Совкомбанк", Country = "Россия" },
            new Broker { Id = Guid.Parse("1634010e-bf7a-4e0c-89c8-643cde8d6009"), Name = "Райффайзенбанк", Country = "Россия" },
            new Broker { Id = Guid.Parse("8f3f0f71-ec6f-4f16-960b-a8440deeb010"), Name = "БКС Мир инвестиций", Country = "Россия" },
            new Broker { Id = Guid.Parse("d040def8-f06d-4602-b350-5c30d9f06011"), Name = "ФИНАМ", Country = "Россия" },
            new Broker { Id = Guid.Parse("20237c6b-3956-4228-ab4e-a4f4c7f1c012"), Name = "АТОН", Country = "Россия" },
            new Broker { Id = Guid.Parse("2f6dd2e6-c1bf-4eec-b024-f7570ab70013"), Name = "КИТ Финанс Брокер", Country = "Россия" },
            new Broker { Id = Guid.Parse("ebff8036-ab31-4e47-ae04-59071f66b014"), Name = "Цифра брокер", Country = "Россия" },
            new Broker { Id = Guid.Parse("2f54daf8-1f36-4428-9580-28c3fe307015"), Name = "Ак Барс Банк", Country = "Россия" },
            new Broker { Id = Guid.Parse("1a9e3959-acf8-4936-a90d-90cb9ce98016"), Name = "Банк Уралсиб", Country = "Россия" },
            new Broker { Id = Guid.Parse("dc8f9da6-5d31-4dd8-a314-6fa3774e3017"), Name = "МТС Банк", Country = "Россия" },
            new Broker { Id = Guid.Parse("677f0251-0b17-48c5-b14b-37f490ec2018"), Name = "Банк Санкт-Петербург", Country = "Россия" },
            new Broker { Id = Guid.Parse("935c6579-7695-43bf-8d8c-d6268bc7f019"), Name = "МКБ", Country = "Россия" },
            new Broker { Id = Guid.Parse("348c1aa0-5480-4fa1-a719-b36eb88dd020"), Name = "Инвестиционная палата", Country = "Россия" }
        );
    }
}
