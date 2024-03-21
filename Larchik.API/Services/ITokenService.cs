using Larchik.Persistence.Entities;

namespace Larchik.API.Services;

public interface ITokenService
{
    public string CreateToken(AppUser user, IEnumerable<string>? roles);
}