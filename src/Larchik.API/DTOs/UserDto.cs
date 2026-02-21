namespace Larchik.API.DTOs;

public record UserDto
{
    public Guid Id { get; init; }
    public string? Email { get; init; }
    public string? Username { get; init; }
    public bool EmailConfirmed { get; init; }
    public IReadOnlyCollection<string> Roles { get; init; } = [];
    public bool IsAdmin => Roles.Contains(Persistence.Constants.Roles.Admin);
};
