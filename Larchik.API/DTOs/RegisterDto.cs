using System.ComponentModel.DataAnnotations;

namespace Larchik.API.DTOs;

public record RegisterDto
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;
    [Required]
    [RegularExpression("(?=.*\\d)(?=.*[a-z])(?=.*[A-Z]).{4,8}$", ErrorMessage = "Password must be stronger")]
    public string Password { get; set; } = string.Empty;
    [Required]
    public string UserName { get; set; } = string.Empty;
};
