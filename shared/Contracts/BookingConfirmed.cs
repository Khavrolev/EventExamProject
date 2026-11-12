namespace Contracts;

public record BookingConfirmed(
    Guid BookingId,
    Guid EventId,
    Guid UserId,
    int SeatsCount,
    DateTime ConfirmedAt
);

public record BookingCancelled(
    Guid BookingId,
    Guid EventId,
    Guid UserId,
    int SeatsCount,
    DateTime CancelledAt
);

public record BookingRejected(
    Guid BookingId,
    Guid EventId,
    string Reason,
    DateTime RejectedAt
);
