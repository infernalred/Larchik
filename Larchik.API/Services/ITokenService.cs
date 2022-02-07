using Larchik.Domain;

namespace Larchik.API.Services;

public interface ITokenService
{
    public string CreateToken(AppUser user);
}