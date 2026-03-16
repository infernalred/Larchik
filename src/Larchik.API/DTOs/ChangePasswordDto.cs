using System.ComponentModel.DataAnnotations;

namespace Larchik.API.DTOs;

public record ChangePasswordDto
{
    [Required]
    public string CurrentPassword { get; init; } = string.Empty;

    [Required]
    [RegularExpression("(?=.*\\d)(?=.*[a-z])(?=.*[A-Z]).{8,}$", ErrorMessage = "Password must be stronger")]
    public string NewPassword { get; init; } = string.Empty;
}
