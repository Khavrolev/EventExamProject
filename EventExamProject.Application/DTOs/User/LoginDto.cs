using System.ComponentModel.DataAnnotations;
using EventExamProject.Application.Resources;

namespace EventExamProject.Application.DTOs.User;

public class LoginDto
{
    [Required(ErrorMessageResourceType = typeof(ValidationMessages),
        ErrorMessageResourceName = "FieldRequired")]
    public required string Login { get; set; }

    [Required(ErrorMessageResourceType = typeof(ValidationMessages),
        ErrorMessageResourceName = "FieldRequired")]
    public required string Password { get; set; }
}
