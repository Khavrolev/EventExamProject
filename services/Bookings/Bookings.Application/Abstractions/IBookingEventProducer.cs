using Contracts;

namespace Bookings.Application.Abstractions;

public interface IBookingEventProducer
{
    Task PublishAsync(string topic, string key, string payload, CancellationToken cancellationToken = default);
    Task PublishBookingConfirmedAsync(BookingConfirmed @event, CancellationToken cancellationToken = default);
    Task PublishBookingCancelledAsync(BookingCancelled @event, CancellationToken cancellationToken = default);
}
