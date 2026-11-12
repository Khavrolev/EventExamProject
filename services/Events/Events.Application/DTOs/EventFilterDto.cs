using Events.Application.Validation;

namespace Events.Application.DTOs;

public class EventFilterDto
{
    public string? Title { get; set; }
    public DateTime? From { get; set; }

    [DateAfter(nameof(From), orEqual: true)]
    public DateTime? To { get; set; }
}
