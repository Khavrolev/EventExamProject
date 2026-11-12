using Contracts;

namespace Bookings.Application.Abstractions;

public interface IBookingEventProducer
{
    Task PublishBookingConfirmedAsync(BookingConfirmed @event, CancellationToken cancellationToken = default);
}
