// using Larchik.Application.Contracts;
// using Larchik.Application.Helpers;
// using Larchik.Persistence.Context;
// using Mapster;
// using MediatR;
// using Microsoft.EntityFrameworkCore;
//
// namespace Larchik.Application.Stocks.UpdateStock;
//
// public class UpdateStockCommandHandler(DataContext context, IUserAccessor userAccessor)
//     : IRequestHandler<UpdateStockCommand, Result<Unit>?>
// {
//     public async Task<Result<Unit>?> Handle(UpdateStockCommand request, CancellationToken cancellationToken)
//     {
//         var stock = await context.Stocks
//             .FirstOrDefaultAsync(x => x.Id == request.Stock.Id, cancellationToken);
//
//         if (stock is null) return null;
//
//         request.Stock.Id = request.Id;
//
//         request.Stock.Adapt(stock);
//
//         stock.UpdatedBy = userAccessor.GetUserId();
//
//         var result = await context.SaveChangesAsync(cancellationToken) > 0;
//
//         return result ? Result<Unit>.Success(Unit.Value) : Result<Unit>.Failure("Failed to update stock");
//     }
// }