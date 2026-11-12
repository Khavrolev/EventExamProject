using System.ComponentModel.DataAnnotations;
using Events.Application.Validation;

namespace Events.Application.DTOs;

public class EventDto
{
    [Required]
    public required string Title { get; set; }

    public string? Description { get; set; }

    [Required]
    public DateTime StartAt { get; set; }

    [Required]
    [DateAfter(nameof(StartAt))]
    public DateTime EndAt { get; set; }

    [Required]
    [Range(1, int.MaxValue)]
    public int? TotalSeats { get; set; }
}
