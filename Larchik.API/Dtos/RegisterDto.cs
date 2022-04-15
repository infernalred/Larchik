using System.ComponentModel.DataAnnotations;

namespace Larchik.API.Dtos;

public class RegisterDto
{
    [Required]
    public string DisplayName { get; set; } = null!;
    [Required]
    [EmailAddress]
    public string Email { get; set; } = null!;
    [Required]
    [RegularExpression("(?=.*\\d)(?=.*[a-z])(?=.*[A-Z]).{4,8}$", ErrorMessage = "Password must be stronger")]
    public string Password { get; set; } = null!;
    [Required]
    public string UserName { get; set; } = null!;
}