using System.ComponentModel.DataAnnotations;

namespace Larchik.API.DTOs;

public record ResetPasswordDto
{
    [Required]
    public Guid UserId { get; init; }

    [Required]
    public string Token { get; init; } = string.Empty;

    [Required]
    [RegularExpression("(?=.*\\d)(?=.*[a-z])(?=.*[A-Z]).{4,8}$", ErrorMessage = "Password must be stronger")]
    public string NewPassword { get; init; } = string.Empty;
}
