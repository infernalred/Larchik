namespace Larchik.API.DTOs;

public record UserDto
{
    public string? Email { get; set; }
    public string Token { get; set; } = null!;
    public string? Username { get; set; }
};