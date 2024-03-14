using Larchik.Persistence.Entity;

namespace Larchik.API.Services;

public interface ITokenService
{
    public string CreateToken(AppUser user);
}