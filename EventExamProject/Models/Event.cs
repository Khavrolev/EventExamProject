using EventExamProject.DTOs.Event;
using System.ComponentModel.DataAnnotations;

namespace EventExamProject.Models;

public class Event
{
    public Guid Id { get; set; }
    public required string Title { get; set; }
    public string? Description { get; set; }
    public DateTime StartAt { get; set; }
    public DateTime EndAt { get; set; }
    public required int TotalSeats { get; set; }
    public required int AvailableSeats { get; set; }

    public static Event Create(EventDto dto)
    {
        if (dto.TotalSeats is null or <= 0)
        {
            throw new ValidationException("TotalSeats must be greater than zero");
        }
        
        return new Event
        {
            Id = Guid.NewGuid(),
            Title = dto.Title,
            Description = dto.Description,
            StartAt = dto.StartAt,
            EndAt = dto.EndAt,
            TotalSeats = dto.TotalSeats.Value,
            AvailableSeats = dto.TotalSeats.Value
        };
    }

    public void Update(EventDto dto)
    {
        Title = dto.Title;
        Description = dto.Description;
        StartAt = dto.StartAt;
        EndAt = dto.EndAt;
    }

    public bool TryReserveSeats(int count = 1)
    {
        if (this.AvailableSeats < count)
        {
            return false;
        }
        
        this.AvailableSeats = this.AvailableSeats - count;
        return true;
    }
    
    public void ReleaseSeats(int count = 1)
    {
        if (this.AvailableSeats + count > this.TotalSeats)
        {
            throw new ValidationException("Cannot release more seats than available");
        }
        
        this.AvailableSeats = this.AvailableSeats + count;
    }
}