using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Larchik.Persistence.Entities;
using Microsoft.IdentityModel.Tokens;

namespace Larchik.API.Services;

public class TokenService(IConfiguration configuration) : ITokenService
{
    public string CreateToken(AppUser user, IEnumerable<string>? roles)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, user.UserName ?? throw new InvalidOperationException()),
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Email, user.Email ?? throw new InvalidOperationException())
        };
        if (roles != null) claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

        var daysTokenLife = configuration.GetSection("DaysTokenLife").Get<int?>() ?? 7;
        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(configuration["TokenKey"] ?? throw new InvalidOperationException()));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha512Signature);

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.AddDays(daysTokenLife),
            SigningCredentials = credentials
        };

        var tokenHandler = new JwtSecurityTokenHandler();

        var token = tokenHandler.CreateToken(tokenDescriptor);

        return tokenHandler.WriteToken(token);
    }
}