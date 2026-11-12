using Events.Domain.Exceptions;
using Events.Domain.ValueObjects;

namespace Events.Domain.Entities;

public class Event
{
    public Guid Id { get; set; }
    public required string Title { get; set; }
    public string? Description { get; set; }
    public DateTime StartAt { get; set; }
    public DateTime EndAt { get; set; }
    public required int TotalSeats { get; set; }
    public required int AvailableSeats { get; set; }

    private Event()
    {
        Title = null!;
    }

    public static Event Create(EventDetails details)
    {
        if (details.TotalSeats <= 0)
        {
            throw new DomainValidationException("TotalSeats must be greater than zero");
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
            throw new DomainValidationException("TotalSeats must be greater than zero");
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
                throw new DomainValidationException("TotalSeats cannot be less than the number of seats already booked");
            }

            AvailableSeats = details.TotalSeats - bookedSeats;
            TotalSeats = details.TotalSeats;
        }
    }

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
        
        this.AvailableSeats -= count;
        return true;
    }
    
    public void ReleaseSeats(int count = 1)
    {
        if (this.AvailableSeats + count > this.TotalSeats)
        {
            throw new DomainValidationException("Cannot release more seats than available");
        }
        
        this.AvailableSeats += count;
    }
}
