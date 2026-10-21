using EventExamProject.Domain.Exceptions;

namespace EventExamProject.Domain.Entities;

public class Booking
{
    public Guid Id { get; set; }
    public Guid EventId { get; set; }
    public Event Event { get; set; } = null!;
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public BookingStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ProcessedAt { get; set; }

    private Booking()
    {
    }

    public static Booking CreatePending(Guid eventId, Guid userId)
    {
        return new Booking
        {
            Id = Guid.NewGuid(),
            EventId = eventId,
            UserId = userId,
            Status = BookingStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };
    }
    
    public void Confirm()
    {
        EnsurePending(nameof(Confirm));

        Status = BookingStatus.Confirmed;
        ProcessedAt = DateTime.UtcNow;
    }
    
    public void Reject()
    {
        EnsurePending(nameof(Reject));

        Status = BookingStatus.Rejected;
        ProcessedAt = DateTime.UtcNow;
    }

    public void Cancel()
    {
        if (Status == BookingStatus.Cancelled)
        {
            throw new DomainValidationException("Booking is already cancelled");
        }

        Status = BookingStatus.Cancelled;
        ProcessedAt = DateTime.UtcNow;
    }

    private void EnsurePending(string operation)
    {
        if (Status != BookingStatus.Pending)
        {
            throw new DomainValidationException(
                $"Cannot {operation.ToLowerInvariant()} a booking in status {Status}: only pending bookings can be processed");
        }
    }
}