using EventExamProject.DTOs.Event;

namespace EventExamProject.Models;

public class Event
{
    public Guid Id { get; set; }
    public required string Title { get; set; }
    public string? Description { get; set; }
    public DateTime StartAt { get; set; }
    public DateTime EndAt { get; set; }

    public static Event Create(EventDto dto)
    {
        return new Event
        {
            Id = Guid.NewGuid(),
            Title = dto.Title,
            Description = dto.Description,
            StartAt = dto.StartAt,
            EndAt = dto.EndAt
        };
    }

    public void Update(EventDto dto)
    {
        Title = dto.Title;
        Description = dto.Description;
        StartAt = dto.StartAt;
        EndAt = dto.EndAt;
    }
}