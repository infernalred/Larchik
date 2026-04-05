using Larchik.Application.Helpers;
using Larchik.Persistence.Entities;
using MediatR;

namespace Larchik.Application.Categories.GetCategories;

public class GetCategoriesQuery : IRequest<Result<Category[]>>;