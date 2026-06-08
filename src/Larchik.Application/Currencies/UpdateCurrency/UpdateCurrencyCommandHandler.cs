using Larchik.Application.Helpers;
using Larchik.Application.Models;
using Larchik.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace Larchik.Application.Currencies.UpdateCurrency;

public class UpdateCurrencyCommandHandler(LarchikContext context)
{
    public async Task<Result<Unit>> Handle(UpdateCurrencyCommand request, CancellationToken cancellationToken)
    {
        var id = CurrencyInputNormalizer.NormalizeId(request.Id);
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

        var currency = await context.Currencies
            .AsTracking()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (currency is null)
        {
            return Result<Unit>.Failure("Валюта не найдена.");
        }

        currency.Name = name;
        await context.SaveChangesAsync(cancellationToken);

        return Result<Unit>.Success(Unit.Value);
    }
}
