using Larchik.Application.Helpers;
using Larchik.Application.Models;
using MediatR;

namespace Larchik.Application.ReferenceData.GetCountries;

public sealed record GetCountriesQuery : IRequest<Result<IReadOnlyCollection<ReferenceItemDto>>>;
