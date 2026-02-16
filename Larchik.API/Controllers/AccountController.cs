using System.Text;
using Larchik.API.DTOs;
using Larchik.API.Services;
using Larchik.Persistence.Constants;
using Larchik.Persistence.Entities;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;

namespace Larchik.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class AccountController(
    UserManager<AppUser> userManager,
    SignInManager<AppUser> signInManager,
    IEmailSender emailSender,
    IConfiguration configuration,
    IAntiforgery antiforgery) : ControllerBase
{
    [AllowAnonymous]
    [HttpGet("antiforgery")]
    public ActionResult<object> GetAntiforgeryToken()
    {
        var tokens = antiforgery.GetAndStoreTokens(HttpContext);
        if (!string.IsNullOrEmpty(tokens.RequestToken))
        {
            Response.Cookies.Append("XSRF-TOKEN", tokens.RequestToken, new CookieOptions
            {
                HttpOnly = false,
                Secure = true,
                SameSite = SameSiteMode.Lax,
                Path = "/"
            });
        }

        return Ok(new { token = tokens.RequestToken });
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

        if (!result.Succeeded) return BadRequest(result.Errors);

        await userManager.AddToRoleAsync(user, Roles.User);

        await SendEmailConfirmationAsync(user);

        return CreatedAtAction(nameof(Me), BuildUserDto(user, [Roles.User]));
    }

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<ActionResult<UserDto>> Login(LoginDto login)
    {
        var user = await userManager.Users.FirstOrDefaultAsync(x => x.Email == login.Email);

        if (user is null) return Unauthorized();

        if (!await userManager.IsEmailConfirmedAsync(user))
        {
            return Unauthorized("Email not confirmed");
        }

        var result = await signInManager.CheckPasswordSignInAsync(user, login.Password, lockoutOnFailure: true);
        if (!result.Succeeded) return Unauthorized();

        await signInManager.SignInAsync(user, login.RememberMe);

        var roles = await userManager.GetRolesAsync(user);

        return Ok(BuildUserDto(user, roles));
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await signInManager.SignOutAsync();
        return NoContent();
    }

    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword(ChangePasswordDto dto)
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null) return Unauthorized();

        var result = await userManager.ChangePasswordAsync(user, dto.CurrentPassword, dto.NewPassword);
        if (!result.Succeeded) return BadRequest(result.Errors);

        await signInManager.RefreshSignInAsync(user);
        return NoContent();
    }

    [AllowAnonymous]
    [HttpGet("confirm-email")]
    public async Task<IActionResult> ConfirmEmail([FromQuery] Guid userId, [FromQuery] string token)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null) return BadRequest("Invalid user");

        var decodedToken = DecodeToken(token);
        var result = await userManager.ConfirmEmailAsync(user, decodedToken);
        if (!result.Succeeded) return BadRequest(result.Errors);

        return NoContent();
    }

    [AllowAnonymous]
    [HttpPost("resend-confirmation")]
    public async Task<IActionResult> ResendConfirmation(ResendConfirmationDto dto)
    {
        var user = await userManager.FindByEmailAsync(dto.Email);
        if (user is null) return NoContent();
        if (await userManager.IsEmailConfirmedAsync(user)) return NoContent();

        await SendEmailConfirmationAsync(user);
        return NoContent();
    }

    [AllowAnonymous]
    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordDto dto)
    {
        var user = await userManager.FindByEmailAsync(dto.Email);
        if (user is null || !await userManager.IsEmailConfirmedAsync(user))
        {
            return NoContent();
        }

        var token = await userManager.GeneratePasswordResetTokenAsync(user);
        var encoded = EncodeToken(token);
        var resetLink = BuildFrontendLink("/auth/reset-password", user.Id, encoded);

        await emailSender.SendEmailAsync(user.Email!, "Reset your password",
            $"Сброс пароля: <a href=\"{resetLink}\">{resetLink}</a>");

        return NoContent();
    }

    [AllowAnonymous]
    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword(ResetPasswordDto dto)
    {
        var user = await userManager.FindByIdAsync(dto.UserId.ToString());
        if (user is null) return BadRequest("Invalid user");

        var token = DecodeToken(dto.Token);
        var result = await userManager.ResetPasswordAsync(user, token, dto.NewPassword);
        if (!result.Succeeded) return BadRequest(result.Errors);

        return NoContent();
    }

    [HttpGet("me")]
    public async Task<ActionResult<UserDto>> Me()
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null) return Unauthorized();

        var roles = await userManager.GetRolesAsync(user);
        return Ok(BuildUserDto(user, roles));
    }

    private async Task SendEmailConfirmationAsync(AppUser user)
    {
        var token = await userManager.GenerateEmailConfirmationTokenAsync(user);
        var encoded = EncodeToken(token);
        var confirmationLink = BuildFrontendLink("/auth/confirm-email", user.Id, encoded);

        await emailSender.SendEmailAsync(user.Email!, "Confirm your email",
            $"Подтвердите почту: <a href=\"{confirmationLink}\">{confirmationLink}</a>");
    }

    private string BuildFrontendLink(string path, Guid userId, string token)
    {
        var origins = configuration.GetSection("Cors")?.GetSection("Origins")?.Value;
        var defaultOrigin = origins?.Split(',').FirstOrDefault(o => !string.IsNullOrWhiteSpace(o));
        var origin = configuration["Frontend:BaseUrl"] ?? defaultOrigin ?? $"{Request.Scheme}://{Request.Host}";
        var qs = QueryString.Create(new Dictionary<string, string?>
        {
            ["userId"] = userId.ToString(),
            ["token"] = token
        });
        return $"{origin.TrimEnd('/')}{path}{qs}";
    }

    private static string EncodeToken(string token) =>
        WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));

    private static string DecodeToken(string token) =>
        Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(token));

    private static UserDto BuildUserDto(AppUser user, IEnumerable<string> roles) =>
        new()
        {
            Id = user.Id,
            Email = user.Email,
            Username = user.UserName,
            EmailConfirmed = user.EmailConfirmed,
            Roles = roles.ToArray()
        };
}
