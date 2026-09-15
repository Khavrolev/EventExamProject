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
    public ICollection<Booking> Bookings { get; set; } = new List<Booking>();

    private Event()
    {
        Title = null!;
    }

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
            StartAt = NormalizeToUtcConvention(dto.StartAt),
            EndAt = NormalizeToUtcConvention(dto.EndAt),
            TotalSeats = dto.TotalSeats.Value,
            AvailableSeats = dto.TotalSeats.Value
        };
    }

    public void Update(EventDto dto)
    {
        if (dto.TotalSeats is null or <= 0)
        {
            throw new ValidationException("TotalSeats must be greater than zero");
        }

        Title = dto.Title;
        Description = dto.Description;
        StartAt = NormalizeToUtcConvention(dto.StartAt);
        EndAt = NormalizeToUtcConvention(dto.EndAt);

        if (dto.TotalSeats.Value != TotalSeats)
        {
            var bookedSeats = TotalSeats - AvailableSeats;

            if (dto.TotalSeats.Value < bookedSeats)
            {
                throw new ValidationException("TotalSeats cannot be less than the number of seats already booked");
            }

            AvailableSeats = dto.TotalSeats.Value - bookedSeats;
            TotalSeats = dto.TotalSeats.Value;
        }
    }

    /// <summary>
    /// StartAt/EndAt are stored as "timestamp with time zone", which Npgsql only accepts
    /// with Kind=Utc. Whatever Kind the client sent (Utc/Local/Unspecified), the value is
    /// normalized to UTC so filtering and sorting stay consistent regardless of input.
    /// </summary>
    internal static DateTime NormalizeToUtcConvention(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };

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