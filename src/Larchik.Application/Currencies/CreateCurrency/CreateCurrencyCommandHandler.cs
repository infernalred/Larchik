using Larchik.Application.Helpers;
using Larchik.Application.Models;
using Larchik.Persistence.Context;
using Larchik.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace Larchik.Application.Currencies.CreateCurrency;

public class CreateCurrencyCommandHandler(LarchikContext context)
{
    public async Task<Result<Unit>> Handle(CreateCurrencyCommand request, CancellationToken cancellationToken)
    {
        var id = CurrencyInputNormalizer.NormalizeId(request.Model.Id);
        if (id is null)
        {
            return Result<Unit>.Failure("Код валюты должен состоять из трёх букв.");
        }

        var name = CurrencyInputNormalizer.NormalizeName(request.Model.Name);
        if (name is null)
        {
            return Result<Unit>.Failure("Укажите название валюты.");
        }

        if (name.Length > 120)
        {
            return Result<Unit>.Failure("Название валюты не должно превышать 120 символов.");
        }

        var exists = await context.Currencies
            .AnyAsync(x => x.Id == id, cancellationToken);
        if (exists)
        {
            return Result<Unit>.Failure("Валюта с таким кодом уже существует.");
        }

        await context.Currencies.AddAsync(new Currency { Id = id, Name = name }, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        return Result<Unit>.Success(Unit.Value);
    }
}
