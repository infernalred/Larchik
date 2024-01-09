namespace Larchik.Application.Contracts;

public interface IUserAccessor
{
    string GetUsername();
    Guid GetUserId();
}