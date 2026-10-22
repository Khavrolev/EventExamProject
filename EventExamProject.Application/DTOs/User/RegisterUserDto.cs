using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using EventExamProject.Application.Resources;
using EventExamProject.Domain.Entities;

namespace EventExamProject.Application.DTOs.User;

public class RegisterUserDto
{
    [Required(ErrorMessageResourceType = typeof(ValidationMessages),
        ErrorMessageResourceName = "FieldRequired")]
    [StringLength(100, MinimumLength = 3)]
    public required string Login { get; set; }

    [Required(ErrorMessageResourceType = typeof(ValidationMessages),
        ErrorMessageResourceName = "FieldRequired")]
    [StringLength(100, MinimumLength = 6)]
    public required string Password { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public UserRole Role { get; set; } = UserRole.User;
}
