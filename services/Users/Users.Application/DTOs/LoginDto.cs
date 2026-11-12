using System.ComponentModel.DataAnnotations;

namespace Users.Application.DTOs;

public class LoginDto
{
    [Required]
    public required string Login { get; set; }

    [Required]
    public required string Password { get; set; }
}
