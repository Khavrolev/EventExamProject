using System.ComponentModel.DataAnnotations;

namespace Users.Application.DTOs;

public class RegisterUserDto
{
    [Required]
    [StringLength(100, MinimumLength = 3)]
    public required string Login { get; set; }

    [Required]
    [StringLength(100, MinimumLength = 6)]
    public required string Password { get; set; }
}
