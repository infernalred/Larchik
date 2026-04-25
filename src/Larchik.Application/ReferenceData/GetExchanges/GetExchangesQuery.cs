using Larchik.Application.Helpers;
using Larchik.Application.Models;
using MediatR;

namespace Larchik.Application.ReferenceData.GetExchanges;

public sealed record GetExchangesQuery : IRequest<Result<IReadOnlyCollection<ReferenceItemDto>>>;
