using EventExamProject.Resources;
using EventExamProject.Validation;
using System.ComponentModel.DataAnnotations;

namespace EventExamProject.DTOs.Event;

public class EventDto
{
    [Required(ErrorMessageResourceType = typeof(ValidationMessages), 
        ErrorMessageResourceName = "FieldRequired")]
    public required string Title { get; set; }
    public string? Description { get; set; }
    [Required(ErrorMessageResourceType = typeof(ValidationMessages),
        ErrorMessageResourceName = "FieldRequired")]
    public DateTime StartAt { get; set; }
    [Required(ErrorMessageResourceType = typeof(ValidationMessages),
        ErrorMessageResourceName = "FieldRequired")]
    [DateAfter(nameof(StartAt), ErrorMessageResourceType = typeof(ValidationMessages),
        ErrorMessageResourceName = "DateGreaterThan")]
    public DateTime EndAt { get; set; }
}