using Larchik.API.DTOs;
using Larchik.API.Services;
using Larchik.Persistence.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Larchik.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class AccountController(UserManager<AppUser> userManager, ITokenService tokenService) : ControllerBase
{
    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<ActionResult<UserDto>> Login(LoginDto login)
    {
        var user = await userManager.Users
            .FirstOrDefaultAsync(x => x.Email == login.Email);

        if (user is null) return Unauthorized();

        var result = await userManager.CheckPasswordAsync(user, login.Password);

        if (!result) return Unauthorized();

        var roles = await userManager.GetRolesAsync(user);

        return CreateUserObject(user, roles);
    }

    [AllowAnonymous]
    [HttpPost("register")]
    public async Task<ActionResult<UserDto>> Register(RegisterDto registerDto)
    {
        if (await userManager.Users.AnyAsync(x => x.UserName == registerDto.UserName))
        {
            ModelState.AddModelError("username", "Username taken");
            return ValidationProblem();
        }

        if (await userManager.Users.AnyAsync(x => x.Email == registerDto.Email))
        {
            ModelState.AddModelError("email", "Email taken");
            return ValidationProblem();
        }

        var user = new AppUser
        {
            Email = registerDto.Email,
            UserName = registerDto.UserName
        };

        var result = await userManager.CreateAsync(user, registerDto.Password);

        if (result.Succeeded)
        {
            return CreateUserObject(user);
        }

        return BadRequest(result.Errors);
    }

    private UserDto CreateUserObject(AppUser user, IEnumerable<string>? roles = default)
    {
        return new UserDto
        {
            Email = user.Email,
            Token = tokenService.CreateToken(user, roles),
            Username = user.UserName
        };
    }
}