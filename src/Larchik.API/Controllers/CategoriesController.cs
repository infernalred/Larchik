using System.Net;
using Larchik.Application.Categories.GetCategories;
using Larchik.Persistence.Entities;
using Microsoft.AspNetCore.Mvc;

namespace Larchik.API.Controllers;

public class CategoriesController(GetCategoriesQueryHandler getCategories) : BaseApiController
{
    [HttpGet]
    [ProducesResponseType(typeof(Category[]), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.Unauthorized)]
    public async Task<ActionResult<Category[]>> GetCategories()
    {
        return HandleResult(await getCategories.Handle(new GetCategoriesQuery(), HttpContext.RequestAborted));
    }
}
