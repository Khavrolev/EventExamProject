namespace Events.Domain.Entities;

public class ProcessedBooking
{
    public Guid BookingId { get; set; }
    public DateTime ProcessedAt { get; set; }
}
