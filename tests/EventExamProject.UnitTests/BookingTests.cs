using Bookings.Domain.Entities;
using Bookings.Domain.Exceptions;
using Xunit;

namespace EventExamProject.UnitTests;

public class BookingTests
{
    [Fact]
    public void CreatePending_SetsInitialValues()
    {
        var eventId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var booking = Booking.CreatePending(eventId, userId);

        Assert.NotEqual(Guid.Empty, booking.Id);
        Assert.Equal(eventId, booking.EventId);
        Assert.Equal(userId, booking.UserId);
        Assert.Equal(BookingStatus.Pending, booking.Status);
        Assert.Null(booking.ProcessedAt);
    }

    [Fact]
    public void Confirm_TransitionsPendingToConfirmed()
    {
        var booking = Booking.CreatePending(Guid.NewGuid(), Guid.NewGuid());

        booking.Confirm();

        Assert.Equal(BookingStatus.Confirmed, booking.Status);
        Assert.NotNull(booking.ProcessedAt);
    }

    [Fact]
    public void Confirm_ThrowsIfAlreadyConfirmed()
    {
        var booking = Booking.CreatePending(Guid.NewGuid(), Guid.NewGuid());
        booking.Confirm();

        Assert.Throws<DomainValidationException>(() => booking.Confirm());
    }

    [Fact]
    public void Cancel_TransitionsToCancelled()
    {
        var booking = Booking.CreatePending(Guid.NewGuid(), Guid.NewGuid());

        booking.Cancel();

        Assert.Equal(BookingStatus.Cancelled, booking.Status);
        Assert.NotNull(booking.ProcessedAt);
    }

    [Fact]
    public void Reject_TransitionsPendingToRejected()
    {
        var booking = Booking.CreatePending(Guid.NewGuid(), Guid.NewGuid());

        booking.Reject();

        Assert.Equal(BookingStatus.Rejected, booking.Status);
        Assert.NotNull(booking.ProcessedAt);
    }

    [Fact]
    public void Reject_TransitionsConfirmedToRejected()
    {
        var booking = Booking.CreatePending(Guid.NewGuid(), Guid.NewGuid());
        booking.Confirm();

        booking.Reject();

        Assert.Equal(BookingStatus.Rejected, booking.Status);
        Assert.NotNull(booking.ProcessedAt);
    }
}
