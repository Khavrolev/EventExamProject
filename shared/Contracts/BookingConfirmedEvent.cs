namespace Contracts;

public record BookingConfirmedEvent(
    Guid BookingId,
    Guid EventId,
    Guid UserId,
    int SeatsCount,
    DateTime ConfirmedAt
);
