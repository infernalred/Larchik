using Larchik.Domain;
using Larchik.Persistence.Models;

namespace Larchik.API.Services;

public interface ITokenService
{
    public string CreateToken(AppUser user);
}