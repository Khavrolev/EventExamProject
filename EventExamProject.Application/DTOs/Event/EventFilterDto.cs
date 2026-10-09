using EventExamProject.Application.Resources;
using EventExamProject.Application.Validation;

namespace EventExamProject.Application.DTOs.Event;

public class EventFilterDto
{
    public string? Title { get; set; }
    public DateTime? From { get; set; }
    [DateAfter(nameof(From), orEqual: true, ErrorMessageResourceType = typeof(ValidationMessages),
        ErrorMessageResourceName = "DateGreaterThan")]
    public DateTime? To { get; set; }
}