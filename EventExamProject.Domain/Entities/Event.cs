using System.ComponentModel.DataAnnotations;
using EventExamProject.Domain.ValueObjects;

namespace EventExamProject.Domain.Entities;

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

    public static Event Create(EventDetails details)
    {
        if (details.TotalSeats <= 0)
        {
            throw new ValidationException("TotalSeats must be greater than zero");
        }

        return new Event
        {
            Id = Guid.NewGuid(),
            Title = details.Title,
            Description = details.Description,
            StartAt = NormalizeToUtcConvention(details.StartAt),
            EndAt = NormalizeToUtcConvention(details.EndAt),
            TotalSeats = details.TotalSeats,
            AvailableSeats = details.TotalSeats
        };
    }

    public void Update(EventDetails details)
    {
        if (details.TotalSeats <= 0)
        {
            throw new ValidationException("TotalSeats must be greater than zero");
        }

        Title = details.Title;
        Description = details.Description;
        StartAt = NormalizeToUtcConvention(details.StartAt);
        EndAt = NormalizeToUtcConvention(details.EndAt);

        if (details.TotalSeats != TotalSeats)
        {
            var bookedSeats = TotalSeats - AvailableSeats;

            if (details.TotalSeats < bookedSeats)
            {
                throw new ValidationException("TotalSeats cannot be less than the number of seats already booked");
            }

            AvailableSeats = details.TotalSeats - bookedSeats;
            TotalSeats = details.TotalSeats;
        }
    }

    /// <summary>
    /// StartAt/EndAt are stored as "timestamp with time zone", which Npgsql only accepts
    /// with Kind=Utc. Whatever Kind the client sent (Utc/Local/Unspecified), the value is
    /// normalized to UTC so filtering and sorting stay consistent regardless of input.
    /// </summary>
    public static DateTime NormalizeToUtcConvention(DateTime value) => value.Kind switch
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