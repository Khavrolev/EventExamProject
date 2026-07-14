using EventExamProject.Resources;
using System.ComponentModel.DataAnnotations;

namespace EventExamProject.DTOs.Event;

public class EventDto : IValidatableObject
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
    public DateTime EndAt { get; set; }
    
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (EndAt <= StartAt)
            yield return new ValidationResult(
                ValidationMessages.EndAtAfterStartAt,
                [nameof(EndAt)]
            );  
    }
}