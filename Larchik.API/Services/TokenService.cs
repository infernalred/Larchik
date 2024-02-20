using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Larchik.Domain;
using Larchik.Persistence.Models;
using Microsoft.IdentityModel.Tokens;

namespace Larchik.API.Services;

public class TokenService(IConfiguration configuration) : ITokenService
{
    public string CreateToken(AppUser user)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, user.UserName ?? throw new InvalidOperationException()),
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Email, user.Email ?? throw new InvalidOperationException())
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8
            .GetBytes(configuration["TokenKey"] ??
                      throw new InvalidOperationException($"{nameof(TokenService)}: TokenKey not found")));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha512Signature);

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.AddDays(7),
            SigningCredentials = creds
        };

        var tokenHandler = new JwtSecurityTokenHandler();

        var token = tokenHandler.CreateToken(tokenDescriptor);

        return tokenHandler.WriteToken(token);
    }
}