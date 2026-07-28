using EventExamProject.DTOs.Event;
using EventExamProject.Resources;
using System.ComponentModel.DataAnnotations;

namespace EventExamProject.Models;

public class Event : EventDto
{
    [Required(ErrorMessageResourceType = typeof(ValidationMessages),
        ErrorMessageResourceName = "FieldRequired")]
    public Guid Id { get; set; }
}