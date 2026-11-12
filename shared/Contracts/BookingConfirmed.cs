namespace Contracts;

public record BookingConfirmed(
    Guid BookingId,
    Guid EventId,
    Guid UserId,
    int SeatsCount,
    DateTime ConfirmedAt
);

public record BookingConfirmedEvent(
    Guid BookingId,
    Guid EventId,
    Guid UserId,
    int SeatsCount,
    DateTime ConfirmedAt
) : BookingConfirmed(BookingId, EventId, UserId, SeatsCount, ConfirmedAt);
