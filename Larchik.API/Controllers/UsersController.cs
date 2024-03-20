using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Larchik.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class UsersController : ControllerBase
{
    [AllowAnonymous]
    [HttpGet("test")]
    public async Task<ActionResult<Guid>> Login()
    {
        return Ok(Guid.NewGuid());
    }
}