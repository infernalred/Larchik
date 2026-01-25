using System.ComponentModel.DataAnnotations;

namespace Larchik.API.DTOs;

public record ForgotPasswordDto
{
    [Required]
    [EmailAddress]
    public string Email { get; init; } = string.Empty;
}
