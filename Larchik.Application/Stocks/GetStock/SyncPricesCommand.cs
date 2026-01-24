using System.Collections.Generic;
using Larchik.Application.Helpers;
using Larchik.Application.Models;
using MediatR;

namespace Larchik.Application.Prices.SyncPrices;

public record SyncPricesCommand(IReadOnlyCollection<PriceModel> Prices) : IRequest<Result<int>>;
